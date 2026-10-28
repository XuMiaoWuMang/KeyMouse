#requires -Version 7
<#
.SYNOPSIS
    把一个窗口（或整个屏幕）截成 PNG，给"我看一眼实际长什么样"用。

.DESCRIPTION
    前端改动不能靠猜。这个脚本把指定的窗口提到前台再截屏，默认裁掉窗口边框只留内容，
    产物落在 artifacts\shots\ 下，文件名带窗口标题与时间戳，方便事后对照。

    三种截法：
      默认            SetForegroundWindow + CopyFromScreen。真实像素，所见即所得；
                      代价是会遮挡别的窗口，且必须先让窗口能被切到前台。
      -PrintWindow    用 WM_PRINT 让窗口自己绘制。窗口被别的窗口盖住、或桌面被锁时也能拿到内容；
                      代价是 WinUI / 分层组合的窗口经常只截到一片空白。
      -FullScreen     整屏，用来记录"当时桌面上是什么样"。

    程序会在截图前尝试把调用进程设为 Per-Monitor V2 感知，否则在高 DPI 下
    窗口矩形是逻辑像素，截出来的图会偏移一块、边缘带桌面。

.PARAMETER Title
    窗口标题的通配符（默认 `*`）。区分大小写：`-cc`。
    同一个标题有多个窗口时取第一个可见的，并打印候选项，方便改用 -Process / -Id 精确指定。

.PARAMETER Process
    进程名（不带 .exe）。和 -Title 一起用时两个条件都要满足。

.PARAMETER Id
    pid。给自动化脚本用：它比标题更稳。

.PARAMETER Client
    只截客户区（去掉标题栏与边框）。给"我只关心内容区对齐"的场景。

.PARAMETER Out
    输出文件。相对路径按仓库根解析；省略时是
    `artifacts\shots\<yyyyMMdd-HHmmss>\<标题>.png`。目录不存在会自动建。

.PARAMETER List
    只列出当前可见窗口（标题 / 进程 / pid / 矩形），不截图。

.EXAMPLE
    pwsh tools\diagnostics\screenshot.ps1 -Title '*流程编辑器*'
    截编辑器窗口，打印产物路径。

.EXAMPLE
    pwsh tools\diagnostics\screenshot.ps1 -Process KeyMouse.FlowEditor -Client
    只截内容区——检查布局时用它，标题栏会干扰对边距的判断。

.EXAMPLE
    pwsh tools\diagnostics\screenshot.ps1 -List
    截图前先看看窗口到底叫什么。
#>
[CmdletBinding()]
param(
    [string]$Title = '*',
    [string]$Process = '',
    [int]$Id = 0,
    [switch]$Client,
    [switch]$PrintWindow,
    [switch]$FullScreen,
    [switch]$CaseSensitive,
    [string]$Out = '',
    [int]$DelayMs = 350,
    [switch]$List
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
Add-Type -AssemblyName System.Drawing

if (-not ('ScreenShot.Win' -as [type])) {
    Add-Type -Namespace ScreenShot -Name Win -MemberDefinition @'
[DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
[DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr ctx);
[DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr p);
[DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
[DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
[DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
[DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
[DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern bool GetWindowTextW(IntPtr h, System.Text.StringBuilder sb, int max);
[DllImport("user32.dll")] public static extern int GetWindowTextLengthW(IntPtr h);
[DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
[DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
[DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr h, out RECT r);
[DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr h, ref POINT p);
[DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
[DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h, int attr, out RECT r, int size);
public delegate bool EnumProc(IntPtr h, IntPtr p);
[StructLayout(LayoutKind.Sequential)] public struct RECT { public int L; public int T; public int R; public int B; }
[StructLayout(LayoutKind.Sequential)] public struct POINT { public int X; public int Y; }
'@
}

try { [void][ScreenShot.Win]::SetProcessDpiAwarenessContext([IntPtr](-4)) }
catch { try { [void][ScreenShot.Win]::SetProcessDPIAware() } catch { } }

function Get-WindowTitle([IntPtr]$h) {
    $len = [ScreenShot.Win]::GetWindowTextLengthW($h)
    if ($len -le 0) { return '' }
    $sb = [Text.StringBuilder]::new($len + 2)
    [void][ScreenShot.Win]::GetWindowTextW($h, $sb, $sb.Capacity)
    return $sb.ToString()
}

function Get-ProcessName([uint32]$id) {
    try { return (Get-Process -Id $id -ErrorAction Stop).ProcessName } catch { return '?' }
}

# 枚举可见的顶层窗口，得到一个普通对象列表，供 -List 和挑选共用。
$windows = [System.Collections.Generic.List[object]]::new()
$script:collector = {
    param([IntPtr]$h, [IntPtr]$p)
    if (-not [ScreenShot.Win]::IsWindowVisible($h)) { return $true }
    $t = Get-WindowTitle $h
    if ([string]::IsNullOrWhiteSpace($t)) { return $true }
    [uint32]$owner = 0
    [void][ScreenShot.Win]::GetWindowThreadProcessId($h, [ref]$owner)
    $windows.Add([pscustomobject]@{
            Handle  = $h
            Title   = $t
            Process = Get-ProcessName $owner
            Id      = [int]$owner
        })
    return $true
}
$cb = [ScreenShot.Win+EnumProc]$script:collector
[void][ScreenShot.Win]::EnumWindows($cb, [IntPtr]::Zero)

if ($List) {
    $fmt = '{0,-8} {1,-28} {2}'
    Write-Host ($fmt -f 'PID', 'PROCESS', 'TITLE') -ForegroundColor DarkGray
    foreach ($w in $windows | Sort-Object Process, Title) {
        Write-Host ($fmt -f $w.Id, $w.Process, $w.Title)
    }
    return
}

$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$shotDir = Join-Path $root "artifacts\shots\$stamp"

function Save-Bitmap($bmp, [string]$name, [string]$outPath) {
    $dir = if ($outPath) { Split-Path -Parent $outPath } else { $shotDir }
    if ($dir -and -not (Test-Path -LiteralPath $dir)) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }
    $path = if ($outPath) { $outPath } else { Join-Path $dir ($name + '.png') }
    try { $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png) }
    finally { $bmp.Dispose() }
    return $path
}

if ($FullScreen) {
    $bounds = [System.Windows.Forms.SystemInformation]::VirtualScreen
    if (-not $bounds) {
        Add-Type -AssemblyName System.Windows.Forms
        $bounds = [System.Windows.Forms.SystemInformation]::VirtualScreen
    }
    $bmp = [System.Drawing.Bitmap]::new($bounds.Width, $bounds.Height)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    try { $g.CopyFromScreen($bounds.X, $bounds.Y, 0, 0, $bmp.Size) } finally { $g.Dispose() }
    $target = if ($Out) { if ([System.IO.Path]::IsPathRooted($Out)) { $Out } else { Join-Path $root $Out } } else { '' }
    $path = Save-Bitmap $bmp 'screen' $target
    Write-Host "screenshot -> $path" -ForegroundColor Green
    return
}

# 挑选目标窗口：标题通配 + 进程名 + pid 三者取交集，先精确后模糊。
$candidates = $windows
if ($Id -gt 0) { $candidates = $candidates | Where-Object { $_.Id -eq $Id } }
if ($Process) { $candidates = $candidates | Where-Object { $_.Process -like $Process } }
$match = $candidates | Where-Object {
    if ($CaseSensitive) { $_.Title -clike $Title }
    else { $_.Title -like $Title -or $_.Title.ToLowerInvariant() -like $Title.ToLowerInvariant() }
} | Select-Object -First 1

if (-not $match) {
    Write-Host "没找到匹配的窗口（-Title '$Title' -Process '$Process' -Id $Id）。当前可见窗口：" -ForegroundColor Yellow
    foreach ($w in $windows | Sort-Object Process, Title) { Write-Host ('  {0,-28} {1,7}  {2}' -f $w.Process, $w.Id, $w.Title) }
    exit 4
}

$h = $match.Handle
if ([ScreenShot.Win]::IsIconic($h)) { [void][ScreenShot.Win]::ShowWindow($h, 9) }   # SW_RESTORE
[void][ScreenShot.Win]::SetForegroundWindow($h)
Start-Sleep -Milliseconds $DelayMs

# DWM 的扩展边框才是"眼睛看到的窗口范围"；GetWindowRect 会把看不见的阴影也算进去。
$rect = [ScreenShot.Win+RECT]::new()
$dwm = [ScreenShot.Win]::DwmGetWindowAttribute($h, 9, [ref]$rect, 16)   # DWMWA_EXTENDED_FRAME_BOUNDS
if ($dwm -ne 0) { [void][ScreenShot.Win]::GetWindowRect($h, [ref]$rect) }

if ($Client) {
    $cr = [ScreenShot.Win+RECT]::new()
    [void][ScreenShot.Win]::GetClientRect($h, [ref]$cr)
    $pt = [ScreenShot.Win+POINT]::new()
    [void][ScreenShot.Win]::ClientToScreen($h, [ref]$pt)
    $rect.L = $pt.X; $rect.T = $pt.Y
    $rect.R = $pt.X + $cr.R; $rect.B = $pt.Y + $cr.B
}

$width = $rect.R - $rect.L
$height = $rect.B - $rect.T
if ($width -le 0 -or $height -le 0) {
    Write-Host "窗口矩形不可用（$width x $height）。它可能最小化了。" -ForegroundColor Red
    exit 5
}

$bmp = [System.Drawing.Bitmap]::new($width, $height)
$g = [System.Drawing.Graphics]::FromImage($bmp)
try {
    if ($PrintWindow) {
        # flag 2 = PW_RENDERFULLCONTENT：让 DirectComposition 的窗口也画出来。
        $hdc = $g.GetHdc()
        try { [void][ScreenShot.Win]::PrintWindow($h, $hdc, 2) } finally { $g.ReleaseHdc($hdc) }
    }
    else {
        $g.CopyFromScreen($rect.L, $rect.T, 0, 0, $bmp.Size)
    }
}
finally { $g.Dispose() }

$name = ($match.Title -replace '[\\/:*?"<>|]', '_')
if ($name.Length -gt 60) { $name = $name.Substring(0, 60) }
if ($Client) { $name += '-client' }
$targetPath = if ($Out) { if ([System.IO.Path]::IsPathRooted($Out)) { $Out } else { Join-Path $root $Out } } else { '' }
$path = Save-Bitmap $bmp $name $targetPath

Write-Host ("screenshot -> {0}  ({1}x{2}, {3} pid {4})" -f $path, $width, $height, $match.Process, $match.Id) -ForegroundColor Green
