#requires -Version 7
<#
    输入注入自检：这台机器（更准确地说：这个会话）到底能不能收到 KeyMouse 打进去的字？

    为什么需要它：桌面冒烟（tests\smoke\run.ps1）的全部断言都建立在"打进去的字真的进了窗口"上。
    在某些环境里 SendInput 会**报告成功但事件被丢弃**——典型是嵌套/沙箱会话、远程桌面被挂在后台、
    或者有程序（输入法候选窗、安全软件）把注入的事件吃掉了。那种情况下冒烟会一口气红 50 项，
    看着像产品坏了，其实只是这台会话收不到输入。这个脚本用一个结论明确的实验把两者区分开。

    做法：开冒烟靶子 → 聚焦 → 剪贴板放一个哨兵 → 用 KeyMouse 打一串字 → Ctrl+A、Ctrl+C →
    读回剪贴板。拿到的正好是那串字，才说明注入真的落地了。

        pwsh tools\dev\check-input.ps1                 # 用 artifacts\cli\KeyMouse.exe
        pwsh tools\dev\check-input.ps1 -Exe path\to\KeyMouse.exe
        pwsh tools\dev\check-input.ps1 -KeepOpen       # 冒烟靶子留着不退（看现场）

    退出码：0 = 注入可用；3 = 注入不可用（收不到输入）；4 = 环境没准备好（exe/靶子缺失）。
    会临时改剪贴板，结束时还原。产物不落盘，没有运行目录。
#>
[CmdletBinding()]
param(
    [string]$Exe,
    [string]$TargetExe,
    [switch]$KeepOpen
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
if (-not $Exe) { $Exe = Join-Path $root 'artifacts\cli\KeyMouse.exe' }
if (-not $TargetExe) { $TargetExe = Join-Path $root 'tests\KeyMouse.SmokeTarget\bin\Release\net10.0-windows\KeyMouse.SmokeTarget.exe' }

foreach ($pair in @(@('KeyMouse.exe', $Exe), @('冒烟靶子', $TargetExe))) {
    if (-not (Test-Path $pair[1])) {
        Write-Host "找不到$($pair[0])：$($pair[1])" -ForegroundColor Red
        Write-Host '先跑 pwsh tools\dev\km.ps1 ba（编译整套）与 pwsh tools\build\build.ps1（发布命令行）。'
        exit 4
    }
}
$Exe = (Resolve-Path $Exe).Path
$TargetExe = (Resolve-Path $TargetExe).Path

# 一句话就能验完的实验，用固定串比随机串更好排障：现场一看就知道是不是它。
$nonce = 'km-inject-5193'
$sentinel = 'km-clipboard-sentinel-5193'
$savedClipboard = $null
$started = $null

function Read-ClipboardText {
    try { return [string](Get-Clipboard -Raw -ErrorAction Stop) } catch { return '' }
}

# 打一串字、全选、复制，看看剪贴板里是不是正好那串字。
function Try-Inject {
    & $Exe key type $nonce --interval 30 2>&1 | Out-Null
    $exit = $LASTEXITCODE
    Start-Sleep -Milliseconds 250
    & $Exe key combo ctrl+a 2>&1 | Out-Null
    & $Exe key combo ctrl+c 2>&1 | Out-Null
    Start-Sleep -Milliseconds 400
    $got = Read-ClipboardText
    if ($exit -ne 0) {
        # 产品自己报错就别赖会话：先照它的话修。
        Write-Host "KeyMouse 自己就失败了（exit=$exit），先修它，别怀疑会话。" -ForegroundColor Red
        exit 3
    }
    return [pscustomobject]@{ Ok = ($got -eq $nonce); Got = $got }
}

try {
    try { $savedClipboard = Read-ClipboardText } catch { $savedClipboard = $null }

    Get-Process KeyMouse.SmokeTarget -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Milliseconds 300
    $started = Start-Process -FilePath $TargetExe -NoNewWindow -PassThru
    Start-Sleep -Milliseconds 1200

    # 靶子必须在前台：注入落到哪个窗口，取决于那一刻谁是前台。
    & $Exe window focus --process KeyMouse.SmokeTarget 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Write-Host '聚焦冒烟靶子失败——桌面上没有可用的靶子窗口。' -ForegroundColor Red
        exit 3
    }

    Set-Clipboard -Value $sentinel
    Start-Sleep -Milliseconds 150

    # 第一次不带任何前置动作直接打：正常情况下应该立刻成功。
    $first = Try-Inject
    if ($first.Ok) {
        Write-Host "输入注入可用：靶子里读回了 '$nonce'。" -ForegroundColor Green
        exit 0
    }

    # 没成，再试一次"先按两次 Shift"——中文输入法在位时，注入的字符会先进候选窗而不落到文档里，
    # 连按两次 Shift 切到英文输入就能打出来。这一步会改动这台机器的输入法状态，
    # 所以只在第一次失败之后才做，并且如实说明是第几次成功的。
    Write-Host '第一次没读到；按两次 Shift 切到英文输入后再试一次…' -ForegroundColor DarkGray
    & $Exe key press shift 2>&1 | Out-Null
    Start-Sleep -Milliseconds 400
    & $Exe key press shift 2>&1 | Out-Null
    Start-Sleep -Milliseconds 600
    Set-Clipboard -Value $sentinel
    $second = Try-Inject
    if ($second.Ok) {
        Write-Host "输入注入可用，但需要先把输入法切到英文（连按两次 Shift）。" -ForegroundColor Yellow
        Write-Host '  跑桌面冒烟之前按两次 Shift 切到英文，否则打字类断言会全红——原因不是产品。'
        exit 0
    }

    Write-Host '输入注入不可用：KeyMouse 报告已发送，但靶子里什么也没收到（切英文输入后仍然如此）。' -ForegroundColor Yellow
    Write-Host "  期望剪贴板：$nonce"
    Write-Host "  实际读到：  $(if ($second.Got) { "'$($second.Got)'" } else { '（空）' })"
    Write-Host ''
    Write-Host '这说明本会话的注入事件被系统丢掉了，不是 KeyMouse 的 bug。常见原因：'
    Write-Host '  · 命令跑在嵌套会话/沙箱里（本仓库的自动化就是在这样的环境里跑的）'
    Write-Host '  · 远程桌面/虚拟机窗口不在前台，或桌面已锁屏'
    Write-Host '  · 前台有输入法候选窗、录屏/安全软件在拦注入'
    Write-Host '换一个真正交互的终端（本机直接开的 Windows Terminal）再跑一次；仍旧失败才是产品问题。'
    exit 3
}
finally {
    if (-not $KeepOpen) {
        Get-Process KeyMouse.SmokeTarget -ErrorAction SilentlyContinue | Stop-Process -Force
    }
    if ($null -ne $savedClipboard) { Set-Clipboard -Value $savedClipboard -ErrorAction SilentlyContinue }
    if ($KeepOpen -and $started) { Write-Host "冒烟靶子留着：pid $($started.Id)" }
}
