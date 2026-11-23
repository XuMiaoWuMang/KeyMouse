<#
.SYNOPSIS
Validate, plan, or explicitly install a signed UIAccess helper (Windows only).
.DESCRIPTION
Plan is read-only and needs no elevation. Install requires an already elevated shell;
this script never elevates itself, installs certificates, or changes certificate stores.
Pass the complete managed-single-file publish directory containing exactly the signed
KeyMouse.Input.exe, onnxruntime.dll and onnxruntime_providers_shared.dll.
.EXAMPLE
pwsh tools/build/install-uiaccess.ps1 -HelperPath C:\signed\Input
.EXAMPLE
pwsh tools/build/install-uiaccess.ps1 -HelperPath C:\signed\Input -Mode Install
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory)][string]$HelperPath,
    [ValidateSet('Plan', 'Install')][string]$Mode = 'Plan'
)
$ErrorActionPreference = 'Stop'

function Get-UIAccessManifest([string]$Path) {
    # LOAD_LIBRARY_AS_DATAFILE | LOAD_LIBRARY_AS_IMAGE_RESOURCE: never execute helper code.
    if (-not ('KeyMouse.UIAccessManifest' -as [type])) {
        Add-Type -TypeDefinition @'
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
namespace KeyMouse {
    public static class UIAccessManifest {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern IntPtr LoadLibraryEx(string path, IntPtr file, uint flags);
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr FindResource(IntPtr module, IntPtr name, IntPtr type);
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr LoadResource(IntPtr module, IntPtr resource);
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern uint SizeofResource(IntPtr module, IntPtr resource);
        [DllImport("kernel32.dll")] static extern IntPtr LockResource(IntPtr resource);
        [DllImport("kernel32.dll")] static extern bool FreeLibrary(IntPtr module);
        public static string Read(string path) {
            var module = LoadLibraryEx(path, IntPtr.Zero, 0x22);
            if (module == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
            try {
                var resource = FindResource(module, new IntPtr(1), new IntPtr(24));
                if (resource == IntPtr.Zero) throw new InvalidOperationException("Missing embedded RT_MANIFEST #1.");
                uint size = SizeofResource(module, resource);
                if (size == 0 || size > 1048576) throw new InvalidOperationException("Invalid manifest size.");
                var bytes = new byte[(int)size];
                var address = LockResource(LoadResource(module, resource));
                if (address == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
                Marshal.Copy(address, bytes, 0, bytes.Length);
                using (var reader = new System.IO.StreamReader(new System.IO.MemoryStream(bytes), Encoding.UTF8, true))
                    return reader.ReadToEnd().TrimEnd('\0');
            } finally { FreeLibrary(module); }
        }
    }
}
'@
    }
    [KeyMouse.UIAccessManifest]::Read($Path)
}

function Assert-UIAccessHelper([string]$Path) {
    if (-not [IO.Path]::IsPathFullyQualified($Path) -or $Path.StartsWith('\\')) {
        throw 'HelperPath must be an absolute local path.'
    }
    $item = Get-Item -LiteralPath $Path -Force
    if ($item.PSIsContainer -or $item.Extension -ine '.exe') { throw 'Helper must be an executable file.' }
    Assert-NoReparsePoint $item.FullName
    $signature = Get-AuthenticodeSignature -LiteralPath $item.FullName
    if ($signature.Status -ne 'Valid') { throw "Helper signature must be Valid; found $($signature.Status)." }
    $settings = [Xml.XmlReaderSettings]::new()
    $settings.DtdProcessing = [Xml.DtdProcessing]::Prohibit
    $settings.XmlResolver = $null
    $reader = [Xml.XmlReader]::Create([IO.StringReader]::new((Get-UIAccessManifest $item.FullName)), $settings)
    try {
        $manifest = [Xml.XmlDocument]::new()
        $manifest.Load($reader)
    } finally { $reader.Dispose() }
    $levels = @($manifest.SelectNodes("//*[local-name()='requestedExecutionLevel']"))
    if ($levels.Count -ne 1 -or $levels[0].GetAttribute('uiAccess') -cne 'true' -or
        $levels[0].GetAttribute('level') -cne 'asInvoker') {
        throw 'Helper manifest must contain one requestedExecutionLevel with level=asInvoker and uiAccess=true.'
    }
    [pscustomobject]@{
        Path = $item.FullName
        Sha256 = (Get-FileHash -LiteralPath $item.FullName -Algorithm SHA256).Hash
        Signer = $signature.SignerCertificate.Subject
    }
}

function Assert-NoReparsePoint([string]$Path) {
    $current = [IO.Path]::GetFullPath($Path)
    while ($current) {
        if (Test-Path -LiteralPath $current) {
            $item = Get-Item -LiteralPath $current -Force
            if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw "Reparse points are not allowed: $current"
            }
        }
        $current = [IO.Path]::GetDirectoryName($current)
    }
}

function Assert-ProtectedAcl([string]$Path, [switch]$Ancestor) {
    $trusted = @('S-1-5-18', 'S-1-5-32-544',
        'S-1-5-80-956008885-3418522649-1831038044-1853292631-2271478464') # SYSTEM, Administrators, TrustedInstaller
    $acl = Get-Acl -LiteralPath $Path
    $owner = $acl.GetOwner([Security.Principal.SecurityIdentifier]).Value
    if ($owner -notin $trusted) { throw "Untrusted owner on protected path: $Path ($owner)" }
    # Check individual mutation bits: Modify is composite and includes harmless read/execute bits.
    # Include native GENERIC_WRITE/GENERIC_ALL because inherited Windows ACEs may retain generic masks.
    [long]$write = 0x500D0156
    if ($Ancestor) {
        # A volume root may permit creating siblings, but must not permit removing/replacing Program Files.
        $write = 0x100D0040
    }
    foreach ($rule in $acl.GetAccessRules($true, $true, [Security.Principal.SecurityIdentifier])) {
        if ($rule.AccessControlType -ne 'Allow' -or
            ($rule.PropagationFlags -band [Security.AccessControl.PropagationFlags]::InheritOnly) -ne 0) { continue }
        if ($rule.IdentityReference.Value -notin $trusted -and ($rule.FileSystemRights -band $write) -ne 0) {
            throw "Untrusted write permission on protected path: $Path ($($rule.IdentityReference.Value))"
        }
    }
}

function Get-UIAccessDestination {
    # SpecialFolder uses the OS known folder, not a caller-supplied ProgramFiles environment variable.
    $programFiles = [Environment]::GetFolderPath([Environment+SpecialFolder]::ProgramFiles)
    if (-not $programFiles -or -not [IO.Path]::IsPathFullyQualified($programFiles)) { throw 'Program Files is unavailable.' }
    Join-Path $programFiles 'KeyMouse\Input\KeyMouse.Input.exe'
}

function Assert-UIAccessDestination([string]$Destination) {
    $expected = Get-UIAccessDestination
    if (-not [string]::Equals($expected, $Destination, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'UIAccess destination must be the fixed Program Files location.'
    }
    Assert-NoReparsePoint $Destination
    $installedDirectory = [IO.Path]::GetDirectoryName($Destination)
    if (Test-Path -LiteralPath $installedDirectory) {
        foreach ($entry in Get-ChildItem -LiteralPath $installedDirectory -Force) {
            Assert-NoReparsePoint $entry.FullName
            if ($entry.PSIsContainer) { throw "Unexpected installed subdirectory: $($entry.FullName)" }
            Assert-ProtectedAcl $entry.FullName
        }
    }
    $programFiles = [Environment]::GetFolderPath([Environment+SpecialFolder]::ProgramFiles)
    $parent = [IO.Path]::GetDirectoryName($programFiles)
    while ($parent) {
        Assert-ProtectedAcl $parent -Ancestor
        $parent = [IO.Path]::GetDirectoryName($parent)
    }
    foreach ($path in @($programFiles, (Join-Path $programFiles 'KeyMouse'),
        (Join-Path $programFiles 'KeyMouse\Input'), $Destination)) {
        if (Test-Path -LiteralPath $path) { Assert-ProtectedAcl $path }
    }
}

function Assert-UIAccessPayload([string]$Path) {
    if (-not [IO.Path]::IsPathFullyQualified($Path) -or $Path.StartsWith('\\')) {
        throw 'HelperPath must be an absolute local publish directory.'
    }
    $directory = Get-Item -LiteralPath $Path -Force
    if (-not $directory.PSIsContainer) { throw 'HelperPath must be the complete publish directory.' }
    Assert-NoReparsePoint $directory.FullName
    # Deliberately narrow native payload contract; new dependencies require a reviewed allowlist change.
    $allowed = @('KeyMouse.Input.exe', 'onnxruntime.dll', 'onnxruntime_providers_shared.dll')
    $files = @()
    foreach ($item in Get-ChildItem -LiteralPath $directory.FullName -Force) {
        Assert-NoReparsePoint $item.FullName
        if ($item.PSIsContainer -or $item.Name -notin $allowed) { throw "Unknown UIAccess payload entry: $($item.Name)" }
        if ($item.Name -ieq 'KeyMouse.Input.exe') {
            $verified = Assert-UIAccessHelper $item.FullName
        } else {
            $signature = Get-AuthenticodeSignature -LiteralPath $item.FullName
            if ($signature.Status -ne 'Valid') { throw "Payload signature must be Valid: $($item.Name) ($($signature.Status))" }
            $verified = [pscustomobject]@{ Path = $item.FullName; Sha256 = (Get-FileHash -LiteralPath $item.FullName -Algorithm SHA256).Hash; Signer = $signature.SignerCertificate.Subject }
        }
        $files += [pscustomobject]@{ Name = $item.Name; Path = $item.FullName; Sha256 = $verified.Sha256; Signer = $verified.Signer }
    }
    foreach ($required in $allowed) {
        if ($required -notin $files.Name) { throw "Payload is missing $required." }
    }
    [pscustomobject]@{ Path = $directory.FullName; Files = @($files | Sort-Object Name) }
}

function Set-ProtectedDirectory([string]$Path) {
    [IO.Directory]::CreateDirectory($Path) | Out-Null
    $acl = [Security.AccessControl.DirectorySecurity]::new()
    $acl.SetAccessRuleProtection($true, $false)
    $acl.SetOwner([Security.Principal.SecurityIdentifier]::new('S-1-5-32-544'))
    foreach ($sid in @('S-1-5-18', 'S-1-5-32-544', 'S-1-5-32-545')) {
        $rights = if ($sid -eq 'S-1-5-32-545') { 'ReadAndExecute' } else { 'FullControl' }
        $acl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new(
            [Security.Principal.SecurityIdentifier]::new($sid), $rights,
            'ContainerInherit, ObjectInherit', 'None', 'Allow'))
    }
    Set-Acl -LiteralPath $Path -AclObject $acl
    Assert-ProtectedAcl $Path
}

function Assert-SamePayload($Expected, $Actual) {
    if (($Expected.Files.Name -join '|') -cne ($Actual.Files.Name -join '|')) { throw 'Payload file set changed.' }
    foreach ($file in $Expected.Files) {
        if ($file.Sha256 -ne ($Actual.Files | Where-Object Name -CEQ $file.Name).Sha256) { throw "Payload hash mismatch: $($file.Name)" }
    }
}

if (-not $IsWindows) { throw 'UIAccess installation requires Windows and PowerShell 7.' }
$helper = Assert-UIAccessPayload $HelperPath
$destination = Get-UIAccessDestination
Assert-UIAccessDestination $destination
if (Test-Path -LiteralPath ([IO.Path]::GetDirectoryName($destination))) {
    Assert-UIAccessPayload ([IO.Path]::GetDirectoryName($destination)) | Out-Null
}
$plan = [pscustomobject]@{
    Mode = $Mode; Source = $helper.Path; Destination = $destination
    Files = $helper.Files
    RequiresAdministrator = $true; CertificateStoreChanges = $false
}
if ($Mode -eq 'Plan') { $plan; return }
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]::new($identity)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Install requires an already elevated PowerShell. No automatic elevation is performed.'
}
if (-not $PSCmdlet.ShouldProcess($destination, 'Install verified UIAccess helper')) { $plan; return }

# Lock every source file against writes/deletion until the verified directory has been installed.
$handles = @{}
$staging = $null
$backup = $null
$directory = [IO.Path]::GetDirectoryName($destination)
try {
    foreach ($file in $helper.Files) {
        $handles[$file.Name] = [IO.File]::Open($file.Path, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
    }
    Assert-SamePayload $helper (Assert-UIAccessPayload $helper.Path)
    $parent = [IO.Path]::GetDirectoryName($directory)
    Assert-UIAccessDestination $destination
    if (-not (Test-Path -LiteralPath $parent)) { Set-ProtectedDirectory $parent }
    Assert-UIAccessDestination $destination
    $staging = Join-Path $parent ('.Input-stage-' + [guid]::NewGuid().ToString('N'))
    Set-ProtectedDirectory $staging
    foreach ($file in $helper.Files) {
        $target = Join-Path $staging $file.Name
        $output = [IO.File]::Open($target, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
        try { $handles[$file.Name].CopyTo($output); $output.Flush($true) } finally { $output.Dispose() }
        $fileAcl = Get-Acl -LiteralPath $target
        $fileAcl.SetOwner([Security.Principal.SecurityIdentifier]::new('S-1-5-32-544'))
        Set-Acl -LiteralPath $target -AclObject $fileAcl
        Assert-ProtectedAcl $target
    }
    Assert-SamePayload $helper (Assert-UIAccessPayload $staging)
    Assert-UIAccessDestination $destination
    if (Test-Path -LiteralPath $directory) {
        # Verify existing content before moving; never preserve or execute an unknown injected dependency.
        Assert-UIAccessPayload $directory | Out-Null
        $backup = Join-Path $parent ('.Input-backup-' + [guid]::NewGuid().ToString('N'))
        [IO.Directory]::Move($directory, $backup)
    }
    try {
        [IO.Directory]::Move($staging, $directory)
        $staging = $null
        Assert-UIAccessDestination $destination
        Assert-SamePayload $helper (Assert-UIAccessPayload $directory)
    } catch {
        # Preserve the previous verified deployment if switching directories fails.
        if ($backup -and -not (Test-Path -LiteralPath $directory)) {
            [IO.Directory]::Move($backup, $directory)
            $backup = $null
        }
        throw
    }
    if ($backup) {
        Assert-NoReparsePoint $backup
        [IO.Directory]::Delete($backup, $true)
        $backup = $null
    }
    $plan
} finally {
    foreach ($handle in $handles.Values) { $handle.Dispose() }
    if ($staging -and [IO.Directory]::Exists($staging)) {
        Assert-NoReparsePoint $staging
        [IO.Directory]::Delete($staging, $true)
    }
}
