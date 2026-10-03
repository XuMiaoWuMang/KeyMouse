#requires -Version 7
<#
    冒烟测试的公共部分：断言函数、目标窗口、编码自检、剪贴板保存。
    由 tests\smoke.ps1 载入；单独运行某个模块时不要直接跑这个文件。
#>

$ErrorActionPreference = 'Stop'
$script:passed = 0
$script:failed = 0

function Check([string]$what, [bool]$ok, [string]$detail = '') {
    if ($ok) { $script:passed++; Write-Host "  ok   $what" }
    else { $script:failed++; Write-Host "  FAIL $what  -> $detail" }
}

if (-not (Test-Path $Exe)) { Write-Error "KeyMouse.exe not found at $Exe - build it first (.\build.ps1)"; exit 1 }
if (-not (Test-Path $TargetExe)) { Write-Error "smoke target not found at $TargetExe - run: dotnet build KeyMouse.sln -c Release"; exit 1 }
$Exe = (Resolve-Path $Exe).Path
$TargetExe = (Resolve-Path $TargetExe).Path
Write-Host "smoke testing $Exe`n"

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
Start-Process -FilePath $TargetExe
$deadline = (Get-Date).AddSeconds(20)
do {
    Start-Sleep -Milliseconds 300
    $null = & $Exe window focus @target 2>&1
} while ($LASTEXITCODE -ne 0 -and (Get-Date) -lt $deadline)
if ($LASTEXITCODE -ne 0) {
    Write-Error "冒烟靶子起不来（window focus exit=$LASTEXITCODE）——先 dotnet build KeyMouse.sln -c Release"
    exit 1
}