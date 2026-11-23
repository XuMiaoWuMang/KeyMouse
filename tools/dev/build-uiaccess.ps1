[CmdletBinding()]
param(
    [string]$OutputDirectory,
    [ValidateSet('win-x64', 'win-arm64')][string]$Runtime = 'win-x64'
)
$ErrorActionPreference = 'Stop'
$root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
if (-not $OutputDirectory) {
    $OutputDirectory = Join-Path $root ('artifacts/uiaccess/' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
}
$output = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $output) { throw "OutputDirectory must be new: $output" }
$build = Join-Path $output 'build'
$publish = Join-Path $output 'publish'
# Native libraries stay beside the signed image in the protected installation directory.
# Never use IncludeNativeLibrariesForSelfExtract: TEMP is not a UIAccess trust boundary.
& dotnet publish (Join-Path $root 'src/KeyMouse.Cli/KeyMouse.Cli.csproj') -c Release -r $Runtime --self-contained true --artifacts-path $build -o $publish '-p:UIAccessFlavor=true' '-p:PublishSingleFile=true' '-p:IncludeNativeLibrariesForSelfExtract=false' '-p:DebugType=None' '-p:DebugSymbols=false'
if ($LASTEXITCODE -ne 0) { throw "UIAccess publish failed: $LASTEXITCODE" }
$original = Join-Path $publish 'KeyMouse.exe'
$renamed = Join-Path $publish 'KeyMouse.Input.exe'
if (-not (Test-Path -LiteralPath $original) -or (Test-Path -LiteralPath $renamed)) { throw 'Unexpected apphost output' }
# Verified absolute paths within the newly created output directory.
Move-Item -LiteralPath $original -Destination $renamed
# Import libraries are linker inputs, never runtime dependencies. Enumerate and verify
# each exact absolute path inside this new publish directory before removing it.
foreach ($name in @('onnxruntime.lib', 'onnxruntime_providers_shared.lib')) {
    $importLibrary = [IO.Path]::GetFullPath((Join-Path $publish $name))
    if ((Split-Path $importLibrary -Parent) -ne $publish) { throw 'Invalid import library path' }
    if (Test-Path -LiteralPath $importLibrary) { Remove-Item -LiteralPath $importLibrary }
}
$files = @(Get-ChildItem -LiteralPath $publish -File -Recurse)
if ($files | Where-Object { $_.DirectoryName -ne $publish -or $_.Extension -notin '.exe', '.dll' }) {
    throw 'Unexpected publish payload: only a flat EXE/DLL directory is supported'
}
# Build inventory is outside the install payload. No signing, certificate installation or run occurs.
$files | Sort-Object Name | ForEach-Object { $_.Name } | Set-Content -LiteralPath (Join-Path $output 'uiaccess-files.txt') -Encoding utf8
Write-Output "Unsigned UIAccess publish directory: $publish"
Write-Output 'Sign the apphost and verify every EXE/DLL before a separately authorized installation.'
