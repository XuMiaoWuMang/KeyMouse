#requires -Version 7
<#
    冒烟测试的公共部分：目标窗口、编码自检、剪贴板保存、等窗口的帮助函数。
    由 tests\smoke\run.ps1 载入；模块不要自己 dot-source 它。

    约定（模块可以直接用）：
        $Exe            被测的 KeyMouse.exe（绝对路径）
        $RunDir         本次运行的目录：临时脚本、截图、报告都放这里
        $target         调 CLI 时传给 --title 的两个参数
        $targetInScript 在脚本行里带引号的 --title（标题里有空格）
        Check <说明> <bool> [细节]    记一条断言
        Wait-ForWindow <正则> [超时毫秒] [稳定毫秒]
        Reset-Target                  把靶子还原并聚焦（模块开头用；模块自己最小化过靶子的话，结尾也要还原）
#>

$ErrorActionPreference = 'Stop'

# ---- 助手：把靶子临时钉在最上层 ------------------------------------------------------
# region 模块要断言"选中的是谁"。冒烟跑在终端里，终端窗口通常正好盖在靶子上，
# 而 region pick --rect 报的是"这个点下面是谁"。最小化终端没用：Windows Terminal 下
# GetConsoleWindow 拿到的是幕后的 ConPTY 假窗口，不是屏幕上那个。所以让靶子自己置顶。


# ---- 先让本进程看得懂真实像素 -------------------------------------------------------
# 这台机器是 150% 缩放。PowerShell 宿主默认不是 DPI 感知的，GetWindowRect / ClientToScreen 报的是被
# 虚拟化过的坐标（1707x960），而 KeyMouse 是 DPI 感知的、用物理像素（2560x1440）——两边算出来的"同一个点"
# 差着一次缩放，region pick --rect 因此会选到别的窗口（实测：选到的是本工具自己的界面）。
if (-not $script:SmokeDpiReady) {
    Add-Type -Namespace KmSmoke -Name Dpi -MemberDefinition '[DllImport("user32.dll")] public static extern bool SetProcessDPIAware();' -ErrorAction SilentlyContinue
    $null = [KmSmoke.Dpi]::SetProcessDPIAware()
    $script:SmokeDpiReady = $true
}
# ---- 助手：暂时藏起挡住某一点的窗口 ------------------------------------------------
# region pick --rect 报的是"这个点下面是谁"。开发时有别的窗口（编辑器、终端、本工具自己的界面）
# 常常正好盖在靶子上；把那个窗口藏起来做检查，结束后原样还原，比要求跑测试的人先手动挪窗口可靠。
function Hide-WindowAt([int]$X, [int]$Y) {
    Add-Type -Namespace KmSmoke -Name Z -MemberDefinition '[DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags); [DllImport("user32.dll")] public static extern IntPtr WindowFromPoint(long point); [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);' -ErrorAction SilentlyContinue
    $point = [long]$X -bor ([long]$Y -shl 32)
    $handle = [KmSmoke.Z]::WindowFromPoint($point)
    if ($handle -eq [IntPtr]::Zero) { return [IntPtr]::Zero }
    $null = [KmSmoke.Z]::ShowWindow($handle, 0)
    Start-Sleep -Milliseconds 200
    return $handle
}

function Restore-Window([IntPtr]$Handle) {
    if ($Handle -eq [IntPtr]::Zero) { return }
    $null = [KmSmoke.Z]::ShowWindow($Handle, 9)
    Start-Sleep -Milliseconds 200
}
function Set-TargetTopmost([bool]$On) {
    Add-Type -Namespace KmSmoke -Name Top -MemberDefinition '[DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);' -ErrorAction SilentlyContinue
    $line = & $Exe window list --process KeyMouse.SmokeTarget 2>&1 | Where-Object { $_ -match '^0x' } | Select-Object -First 1
    if (-not $line) { return $false }
    $handle = [IntPtr]([Convert]::ToInt64((($line -split '\s+')[0]), 16))
    # -1 = HWND_TOPMOST，-2 = HWND_NOTOPMOST，0x0003 = NOMOVE|NOSIZE
    $after = [IntPtr]$(if ($On) { -1 } else { -2 })
    $null = [KmSmoke.Top]::SetWindowPos($handle, $after, 0, 0, 0, 0, 0x0003)
    Start-Sleep -Milliseconds 250
    return $true
}

# 单独跑某个模块调试时没有 run.ps1 给的目录，退回系统临时目录。
if (-not $RunDir) { $RunDir = $env:TEMP }

# The exe answers in the console's code page, and PowerShell decodes native output with
# [Console]::OutputEncoding - the two agree by default, so nothing is forced here. A host that
# has pinned them apart would otherwise fail every Chinese assertion for a reason that has
# nothing to do with the program, so check the round-trip once and say so plainly.
$probe = (& $Exe mouse move 1 1 2>&1) -join ''
if ($probe -notmatch '已移动到') {
    Write-Host "  cannot decode the program's Chinese output."
    Write-Host "  expected [已移动到 1,1] but got [$probe]"
    Write-Host "  console code page $(try { (chcp) } catch { '?' })" +
                " vs [Console]::OutputEncoding $([Console]::OutputEncoding.WebName)"
    exit 1
}
Check 'Chinese output decodes correctly in this host' $true

$savedClipboard = Get-Clipboard -Raw -ErrorAction SilentlyContinue
$targetTitle = 'KeyMouse 冒烟靶子'
$target = @('--title', $targetTitle)                 # when calling the tool directly
$targetInScript = '--title "' + $targetTitle + '"'   # inside a script line the space needs quotes

# ---- 助手：等某个窗口出现 --------------------------------------------------------
# 固定睡眠在覆盖层这种"起来需要多久取决于抢前台快不快"的东西上最不可靠：聚焦现在会先接线程输入队列
# （最多多花约 0.4 秒），1500ms 的睡眠就从"稳"变成了"偶尔早到"。等窗口出现才是稳的。
function Wait-ForWindow([string]$Pattern, [int]$TimeoutMs = 8000, [int]$SettleMs = 250) {
    $deadline = (Get-Date).AddMilliseconds($TimeoutMs)
    do {
        if ((& $Exe window list --all 2>&1 | Out-String) -match $Pattern) {
            Start-Sleep -Milliseconds $SettleMs   # 窗口在了，再给它一点时间装好鼠标捕获
            return $true
        }
        Start-Sleep -Milliseconds 150
    } while ((Get-Date) -lt $deadline)
    return $false
}

# ---- 基建：冒烟靶子 ---------------------------------------------------------------
# 每个模块都可能要驱动它，所以放在公共部分；entry 模块只负责断言它能起来。
Get-Process KeyMouse.SmokeTarget -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Process -FilePath $TargetExe -NoNewWindow
$deadline = (Get-Date).AddSeconds(20)
do {
    Start-Sleep -Milliseconds 300
    $null = & $Exe window focus @target 2>&1
} while ($LASTEXITCODE -ne 0 -and (Get-Date) -lt $deadline)
if ($LASTEXITCODE -ne 0) {
    Write-Error "冒烟靶子起不来（window focus exit=$LASTEXITCODE）——先 dotnet build KeyMouse.sln -c Release"
    exit 1
}

# ---- 助手：把靶子拉回"看得见、在前台" ----------------------------------------------
# 冒烟模块是一个接一个跑的，前一个模块可能为了自己的断言把靶子最小化了（focus 模块就会）。
# 不还原的话，后面每个模块都会撞在"窗口已最小化"上——一片红，但根因只有一个。
function Reset-Target {
    Add-Type -Namespace KmSmoke -Name Native -MemberDefinition `
        '[DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);' -ErrorAction SilentlyContinue
    $line = & $Exe window list --process KeyMouse.SmokeTarget 2>&1 | Where-Object { $_ -match '^0x' } | Select-Object -First 1
    if ($line) {
        $handle = [IntPtr]([Convert]::ToInt64((($line -split '\s+')[0]), 16))
        $null = [KmSmoke.Native]::ShowWindow($handle, 9)   # SW_RESTORE
        Start-Sleep -Milliseconds 200
    }
    $null = & $Exe window focus @target 2>&1
    return ($LASTEXITCODE -eq 0)
}

