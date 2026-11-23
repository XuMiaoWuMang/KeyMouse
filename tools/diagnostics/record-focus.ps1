#requires -Version 7
<# Real desktop + owned SmokeTarget record/replay acceptance; see docs/tools/record-focus.md. #>
[CmdletBinding()]
param(
    [string]$Exe,
    [string]$TargetExe,
    [ValidateRange(2000, 15000)][int]$DurationMs = 6000,
    [ValidateRange(1000, 10000)][int]$ReadyTimeoutMs = 4000,
    [ValidateRange(1000, 15000)][int]$StopTimeoutMs = 5000,
    [ValidateRange(2000, 30000)][int]$ReplayTimeoutMs = 15000
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$run = Join-Path $root ('artifacts\tests\record-focus-{0}-{1}' -f (Get-Date -Format 'yyyyMMdd-HHmmss-fff'), [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $run | Out-Null
$resultPath = Join-Path $run 'result.json'
$owned = [Collections.Generic.List[object]]::new()
$result = [ordered]@{
    status = 'running'; startedAt = (Get-Date).ToString('o'); outputDirectory = $run
    noShots = $true; desktop = [ordered]@{ status = 'not-run' }; ordinary = [ordered]@{ status = 'not-run' }
    issues = [Collections.Generic.List[string]]::new(); cleanup = @()
}
$desktopToggled = $false
$originalCursor = $null
$previousDpi = [IntPtr]::Zero

# No title/text/clipboard/screenshot APIs. MSAA reads only role + child id, to reject desktop icons.
function Initialize-Native {
    if ('RecordFocusNativeV1' -as [type]) { return }
    Add-Type -AssemblyName Accessibility
    # Accessibility targets System.Runtime 4; this metadata warning is expected on current PowerShell/.NET.
    Add-Type -CompilerOptions '/nowarn:1701' -ReferencedAssemblies ([Accessibility.IAccessible].Assembly.Location) -TypeDefinition @'
using System;
using System.Text;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Accessibility;
public static class RecordFocusNativeV1 {
    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int L, T, R, B; }
    [StructLayout(LayoutKind.Sequential)] struct MOUSEINPUT { public int X,Y; public uint Data,Flags,Time; public IntPtr Extra; }
    [StructLayout(LayoutKind.Sequential)] struct KEYBDINPUT { public ushort Key,Scan; public uint Flags,Time; public IntPtr Extra; }
    [StructLayout(LayoutKind.Explicit)] struct UNION { [FieldOffset(0)] public MOUSEINPUT Mouse; [FieldOffset(0)] public KEYBDINPUT Key; }
    [StructLayout(LayoutKind.Sequential)] struct INPUT { public uint Type; public UNION U; }
    public sealed class Hit { public int X,Y,Role=-1; public string LeafClass="",RootClass=""; public bool Blank; }
    delegate bool EnumProc(IntPtr h,IntPtr p);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb,IntPtr p);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h,out uint pid);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetClassName(IntPtr h,StringBuilder s,int n);
    [DllImport("user32.dll")] static extern IntPtr WindowFromPoint(POINT p);
    [DllImport("user32.dll")] static extern IntPtr GetAncestor(IntPtr h,uint flags);
    [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT p);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x,int y);
    [DllImport("user32.dll")] static extern int GetSystemMetrics(int n);
    [DllImport("user32.dll")] static extern bool GetClientRect(IntPtr h,out RECT r);
    [DllImport("user32.dll")] static extern bool ClientToScreen(IntPtr h,ref POINT p);
    [DllImport("user32.dll")] public static extern bool MoveWindow(IntPtr h,int x,int y,int w,int height,bool repaint);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
    [DllImport("user32.dll",SetLastError=true)] static extern uint SendInput(uint n,INPUT[] inputs,int size);
    [DllImport("oleacc.dll")] static extern int AccessibleObjectFromPoint(POINT p,out IAccessible obj,[MarshalAs(UnmanagedType.Struct)] out object child);
    public static string ClassOf(IntPtr h) { var s=new StringBuilder(256); GetClassName(h,s,256); return s.ToString(); }
    public static IntPtr RootAt(int x,int y) { return GetAncestor(WindowFromPoint(new POINT{X=x,Y=y}),2); }
    public static bool OwnedAt(int x,int y,int pid) { uint owner; GetWindowThreadProcessId(RootAt(x,y),out owner); return owner==pid; }
    public static IntPtr OwnWindow(int pid) {
        IntPtr found=IntPtr.Zero;
        EnumWindows((h,p)=>{ uint owner; GetWindowThreadProcessId(h,out owner); if(owner==pid && IsWindowVisible(h)){found=h;return false;}return true; },IntPtr.Zero);
        return found;
    }
    public static POINT ClientPoint(IntPtr h) {
        RECT r; if(!GetClientRect(h,out r)) throw new Exception("GetClientRect failed");
        var p=new POINT{X=(r.R-r.L)/2,Y=(r.B-r.T)/2};
        if(!ClientToScreen(h,ref p)) throw new Exception("ClientToScreen failed"); return p;
    }
    public static Hit DesktopHit(int x,int y) {
        var p=new POINT{X=x,Y=y}; var leaf=WindowFromPoint(p); var root=GetAncestor(leaf,2);
        var hit=new Hit{X=x,Y=y,LeafClass=ClassOf(leaf),RootClass=ClassOf(root)};
        if(hit.RootClass!="Progman" && hit.RootClass!="WorkerW") return hit;
        if(hit.LeafClass=="Progman" || hit.LeafClass=="WorkerW") {hit.Blank=true;return hit;}
        if(hit.LeafClass!="SysListView32" && hit.LeafClass!="SHELLDLL_DefView") return hit;
        IAccessible obj=null; object child;
        try {
            if(AccessibleObjectFromPoint(p,out obj,out child)!=0 || obj==null || !(child is int) || (int)child!=0) return hit;
            hit.Role=Convert.ToInt32(obj.get_accRole(child));
            hit.Blank=hit.Role==0x21 || hit.Role==0x10 || hit.Role==0x0A || hit.Role==0x09;
        } catch(COMException) { return hit; }
        finally { if(obj!=null) Marshal.ReleaseComObject(obj); }
        return hit;
    }
    // Nine read-only candidates; each needs a clear 32px movement corridor. No clicking while searching.
    public static Hit FindDesktop() {
        int w=GetSystemMetrics(0),h=GetSystemMetrics(1);
        foreach(int x in new[]{w/2,w*3/4,w*7/8}) foreach(int y in new[]{h/2,h*3/4,h/4}) {
            bool clear=true;
            for(int i=0;i<=4;i++) if(!DesktopHit(x+i*8,y+i*4).Blank){clear=false;break;}
            if(clear) return DesktopHit(x,y);
        }
        return null;
    }
    static void Send(INPUT[] inputs) {
        if(SendInput((uint)inputs.Length,inputs,Marshal.SizeOf<INPUT>())!=inputs.Length)
            throw new Exception("SendInput did not deliver all inputs (Win32 "+Marshal.GetLastWin32Error()+")");
    }
    public static void Click() {
        Send(new[]{new INPUT{U=new UNION{Mouse=new MOUSEINPUT{Flags=2}}},new INPUT{U=new UNION{Mouse=new MOUSEINPUT{Flags=4}}}});
    }
    public static void MoveTo(int x,int y) {
        int vx=GetSystemMetrics(76),vy=GetSystemMetrics(77),w=GetSystemMetrics(78),h=GetSystemMetrics(79);
        int nx=(int)Math.Round((x-vx+0.5)*65536.0/w),ny=(int)Math.Round((y-vy+0.5)*65536.0/h);
        Send(new[]{new INPUT{U=new UNION{Mouse=new MOUSEINPUT{X=nx,Y=ny,Flags=0xC001}}}});
        POINT p; if(!GetCursorPos(out p) || p.X!=x || p.Y!=y) throw new Exception("Injected move did not reach the verified point");
    }
    public static void ToggleDesktop() {
        Send(new[]{new INPUT{Type=1,U=new UNION{Key=new KEYBDINPUT{Key=0x5B}}},
            new INPUT{Type=1,U=new UNION{Key=new KEYBDINPUT{Key=0x44}}},
            new INPUT{Type=1,U=new UNION{Key=new KEYBDINPUT{Key=0x44,Flags=2}}},
            new INPUT{Type=1,U=new UNION{Key=new KEYBDINPUT{Key=0x5B,Flags=2}}}});
    }
}
'@
}

function Start-Owned([string]$Name, [string]$File, [string[]]$Arguments = @()) {
    $info = [Diagnostics.ProcessStartInfo]::new($File)
    $info.UseShellExecute = $false; $info.CreateNoWindow = $true; $info.WorkingDirectory = $root
    $info.RedirectStandardOutput = $true; $info.RedirectStandardError = $true
    $info.StandardOutputEncoding = [Text.Encoding]::UTF8; $info.StandardErrorEncoding = [Text.Encoding]::UTF8
    $info.Environment['KEYMOUSE_OUTPUT_ENCODING'] = 'utf-8'
    foreach ($argument in $Arguments) { $info.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::new(); $process.StartInfo = $info
    if (-not $process.Start()) { throw "Could not start $Name" }
    $entry = [pscustomobject]@{
        Name = $Name; Process = $process; Pid = $process.Id; StopSignal = $null
        Stdout = $process.StandardOutput.ReadToEndAsync(); Stderr = $process.StandardError.ReadToEndAsync()
    }
    $owned.Add($entry)
    # Keep the original process handle, so cleanup never reopens a possibly reused PID.
    $null = $process.SafeHandle
    return $entry
}

function Wait-Owned($Entry, [int]$TimeoutMs) {
    if (-not $Entry.Process.WaitForExit($TimeoutMs)) { throw "$($Entry.Name) timed out after ${TimeoutMs}ms" }
    if ($Entry.Process.ExitCode -ne 0) { throw "$($Entry.Name) exited $($Entry.Process.ExitCode); see its stdout/stderr logs" }
}

function Start-Recording([string]$Name, [string]$Flow) {
    $entry = Start-Owned "$Name-record" $script:Exe @('record', '--out', $Flow, '--no-shots', '--duration', "$DurationMs")
    $clock = [Diagnostics.Stopwatch]::StartNew()
    while ($clock.ElapsedMilliseconds -lt $ReadyTimeoutMs) {
        if ($entry.Process.HasExited) { throw "$Name recorder exited before initialization" }
        try { $entry.StopSignal = [Threading.EventWaitHandle]::OpenExisting("Local\KeyMouse-record-stop-$($entry.Pid)") } catch [Threading.WaitHandleCannotBeOpenedException] { }
        if ($entry.StopSignal) {
            # The event is installed just before the hooks. Recorded click/move assertions below prove hook delivery.
            Start-Sleep -Milliseconds 250
            if ($entry.Process.HasExited) { throw "$Name recorder exited before input" }
            return $entry
        }
        Start-Sleep -Milliseconds 40
    }
    throw "$Name recorder stop event was not available within ${ReadyTimeoutMs}ms"
}

function Assert-Point([int]$X, [int]$Y, [int]$OwnPid) {
    if ($OwnPid) {
        if (-not [RecordFocusNativeV1]::OwnedAt($X, $Y, $OwnPid)) { throw 'Point is no longer inside the owned SmokeTarget' }
    } elseif (-not [RecordFocusNativeV1]::DesktopHit($X, $Y).Blank) { throw 'Point is no longer verified blank shell desktop' }
}

function Record-Replay([string]$Name, [int]$X, [int]$Y, [int]$OwnPid = 0, [string]$WindowClass = '') {
    $flowPath = Join-Path $run "$Name.flow.json"
    if (-not [RecordFocusNativeV1]::SetCursorPos($X, $Y)) { throw 'SetCursorPos failed' }
    Start-Sleep -Milliseconds 100
    $recorder = Start-Recording $Name $flowPath
    Assert-Point $X $Y $OwnPid
    $firstHit = if ($OwnPid) { @{ kind = 'ordinary'; ownPid = $OwnPid; rootClass = $WindowClass } } else { [RecordFocusNativeV1]::DesktopHit($X, $Y) }
    $result[$Name] = [ordered]@{ status = 'recording'; firstTarget = $(if ($OwnPid) { 'ordinary' } else { 'desktop' }); firstHit = $firstHit; flow = $flowPath; recordPid = $recorder.Pid }
    [RecordFocusNativeV1]::Click()
    Start-Sleep -Milliseconds 120
    Assert-Point ($X + 32) ($Y + 16) $OwnPid
    [RecordFocusNativeV1]::MoveTo(($X + 32), ($Y + 16))
    Start-Sleep -Milliseconds 150
    $null = $recorder.StopSignal.Set()
    Wait-Owned $recorder $StopTimeoutMs

    $flow = Get-Content -LiteralPath $flowPath -Raw | ConvertFrom-Json -AsHashtable
    $steps = @($flow.steps)
    $result[$Name].status = 'checking-recording'; $result[$Name].recordExitCode = $recorder.Process.ExitCode
    if ($flow.options.shots -ne $false) { throw "$Name unexpectedly enabled screenshots" }
    $focus = @($steps | Where-Object type -eq 'focus')
    $result[$Name].focusCount = $focus.Count; $result[$Name].firstStep = $(if ($steps.Count) { $steps[0] } else { $null })
    if ($focus.Count) { throw "$Name recording contains an automatic focus step" }
    if (-not $steps.Count -or $steps[0].type -notin @('click', 'move')) { throw "$Name recording does not start with actual click/move input" }
    if (@($steps | Where-Object { $_.type -notin @('click', 'move', 'sleep') }).Count) { throw "$Name captured unexpected input; refusing playback" }
    $inputs = @($steps | Where-Object { $_.type -in @('click', 'move') })
    $clicks = @($steps | Where-Object { $_.type -eq 'click' -and ($_.button -in @($null, 'left')) -and $_.at.space -eq 'screen' -and $_.at.x -eq $X -and $_.at.y -eq $Y })
    $moves = @($steps | Where-Object { $_.type -eq 'move' -and $_.at.x -eq ($X + 32) -and $_.at.y -eq ($Y + 16) })
    if (-not $clicks.Count -or -not $moves.Count) { throw "$Name did not record the injected real click and move" }
    if ($inputs[0].at.x -ne $X -or $inputs[0].at.y -ne $Y) { throw "$Name first recorded input did not hit the verified starting point" }
    foreach ($inputStep in $inputs) {
        if ($inputStep.at.space -ne 'screen' -or $inputStep.ContainsKey('target')) { throw "$Name input is not an unbound screen coordinate" }
        if ($inputStep.at.x -lt $X -or $inputStep.at.x -gt ($X + 32) -or $inputStep.at.y -lt $Y -or $inputStep.at.y -gt ($Y + 16)) { throw "$Name contains unexpected coordinates; refusing playback" }
        Assert-Point $inputStep.at.x $inputStep.at.y $OwnPid
    }
    $reportPath = Join-Path $run "$Name.replay-report.json"
    $replay = Start-Owned "$Name-replay" $script:Exe @('run', $flowPath, '--report', $reportPath)
    Wait-Owned $replay $ReplayTimeoutMs
    return [ordered]@{
        status = 'passed'; firstTarget = $(if ($OwnPid) { 'ordinary' } else { 'desktop' }); firstHit = $firstHit
        firstStep = $steps[0]; firstInput = $inputs[0]; clickCount = $clicks.Count; moveCount = $moves.Count; focusCount = $focus.Count
        flow = $flowPath
        recordPid = $recorder.Pid; recordExitCode = $recorder.Process.ExitCode
        replayPid = $replay.Pid; replayExitCode = $replay.Process.ExitCode; replayReport = $reportPath
    }
}

try {
    if (-not $IsWindows -or -not [Environment]::UserInteractive) { throw 'An interactive Windows desktop is required' }
    if (-not $Exe) { $Exe = Join-Path $root 'artifacts\cli\KeyMouse.exe' }
    if (-not $TargetExe) { $TargetExe = Join-Path $root 'tests\KeyMouse.SmokeTarget\bin\Release\net10.0-windows\KeyMouse.SmokeTarget.exe' }
    $Exe = (Resolve-Path -LiteralPath $Exe).Path; $TargetExe = (Resolve-Path -LiteralPath $TargetExe).Path
    $result.cli = @{ path = $Exe; sha256 = (Get-FileHash -LiteralPath $Exe -Algorithm SHA256).Hash }
    Initialize-Native
    $previousDpi = [RecordFocusNativeV1]::SetThreadDpiAwarenessContext([IntPtr]::new(-4))
    $cursor = [RecordFocusNativeV1+POINT]::new()
    if ([RecordFocusNativeV1]::GetCursorPos([ref]$cursor)) { $originalCursor = $cursor }
    try {
        $desktop = [RecordFocusNativeV1]::FindDesktop()
        if (-not $desktop) {
            [RecordFocusNativeV1]::ToggleDesktop(); $desktopToggled = $true
            Start-Sleep -Milliseconds 600
            $desktop = [RecordFocusNativeV1]::FindDesktop()
        }
        if (-not $desktop) { throw 'No verified blank desktop corridor among nine candidates after Win+D; no click was sent' }
        $result.desktop = Record-Replay 'desktop' $desktop.X $desktop.Y
    } finally {
        if ($desktopToggled) { [RecordFocusNativeV1]::ToggleDesktop(); $desktopToggled = $false; Start-Sleep -Milliseconds 300 }
    }
    $target = Start-Owned 'smoke-target' $TargetExe
    $clock = [Diagnostics.Stopwatch]::StartNew(); $window = [IntPtr]::Zero
    while ($clock.ElapsedMilliseconds -lt $ReadyTimeoutMs) {
        if ($target.Process.HasExited) { throw 'Owned SmokeTarget exited before showing its window' }
        $window = [RecordFocusNativeV1]::OwnWindow($target.Pid)
        if ($window -ne [IntPtr]::Zero) { break }
        Start-Sleep -Milliseconds 40
    }
    if ($window -eq [IntPtr]::Zero) { throw 'Owned SmokeTarget did not show a window within the readiness timeout' }
    if (-not [RecordFocusNativeV1]::MoveWindow($window, 100, 100, 800, 500, $true)) { throw 'Could not position owned SmokeTarget' }
    $null = [RecordFocusNativeV1]::SetForegroundWindow($window)
    Start-Sleep -Milliseconds 250
    if ([RecordFocusNativeV1]::GetForegroundWindow() -ne $window) { throw 'Owned SmokeTarget could not become foreground; no input sent' }
    $point = [RecordFocusNativeV1]::ClientPoint($window)
    $result.ordinary = Record-Replay 'ordinary' $point.X $point.Y $target.Pid ([RecordFocusNativeV1]::ClassOf($window))
    $result.status = 'passed'
} catch {
    $result.status = 'failed'; $result.issues.Add($_.Exception.Message)
} finally {
    if ($desktopToggled) {
        try { [RecordFocusNativeV1]::ToggleDesktop(); $desktopToggled = $false } catch { $result.issues.Add("Desktop restore failed: $($_.Exception.Message)") }
    }
    $cleanup = foreach ($entry in $owned) {
        $method = 'already-exited'; $cleanupError = $null
        try {
            if (-not $entry.Process.HasExited) {
                if ($entry.StopSignal) { $null = $entry.StopSignal.Set(); $method = 'stop-event' }
                else { $null = $entry.Process.CloseMainWindow(); $method = 'close-own-window' }
                if (-not $entry.Process.WaitForExit(1200)) { $entry.Process.Kill(); $method = 'kill-held-process'; $null = $entry.Process.WaitForExit(2000) }
            }
            foreach ($stream in @('Stdout', 'Stderr')) {
                if (-not $entry.$stream.Wait(1000)) { throw "$stream did not close within 1000ms" }
                Set-Content -LiteralPath (Join-Path $run "$($entry.Name).$($stream.ToLowerInvariant()).log") -Value $entry.$stream.Result -Encoding utf8
            }
        } catch { $cleanupError = $_.Exception.Message; $result.issues.Add("Cleanup $($entry.Name): $cleanupError") }
        $exited = $entry.Process.HasExited
        [pscustomobject]@{ name = $entry.Name; pid = $entry.Pid; exited = $exited; exitCode = $(if ($exited) { $entry.Process.ExitCode } else { $null }); method = $method; error = $cleanupError }
        if ($entry.StopSignal) { $entry.StopSignal.Dispose() }
        $entry.Process.Dispose()
    }
    $result.cleanup = @($cleanup)
    $result.allOwnedProcessesExited = @($result.cleanup | Where-Object { -not $_.exited }).Count -eq 0
    if ($originalCursor) { $null = [RecordFocusNativeV1]::SetCursorPos($originalCursor.X, $originalCursor.Y) }
    if ($previousDpi -ne [IntPtr]::Zero) { $null = [RecordFocusNativeV1]::SetThreadDpiAwarenessContext($previousDpi) }
    $result.desktopRestored = -not $desktopToggled
    if ($result.issues.Count -or -not $result.allOwnedProcessesExited) { $result.status = 'failed' }
    $result.finishedAt = (Get-Date).ToString('o')
    $result | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $resultPath -Encoding utf8
}
Write-Output "$($result.status): $resultPath"
if ($result.status -ne 'passed') { foreach ($issue in $result.issues) { Write-Output "  $issue" }; exit 1 }
