#requires -Version 7
<#
    Desktop smoke runner: drives a real window through KeyMouse and reads results back through the
    clipboard. Needs an interactive desktop session.

        pwsh tests\smoke.ps1                       # 全部模块（= 总测试的一部分，发布前跑这个）
        pwsh tests\smoke.ps1 -Only loops,flowloops # 只跑改到的模块（平时用这个，快）
        pwsh tests\smoke.ps1 -List                 # 看有哪些模块

    每个模块是 tests\smoke\ 下的一个文件，靠 common.ps1 提供的 Check / $Exe / $target 工作。
    模块按列表顺序执行：`entry` 负责启动冒烟靶子，后面的模块都往它里面打字。
    It never touches the applications you have open, and it restores your clipboard.
    Exit code 0 = all checks passed.
#>
[CmdletBinding()]
param(
    [string]$Exe = (Join-Path $PSScriptRoot '..\dist\KeyMouse.exe'),
    [string]$TargetExe = (Join-Path $PSScriptRoot 'KeyMouse.SmokeTarget\bin\Release\net10.0-windows\KeyMouse.SmokeTarget.exe'),
    [string[]]$Only,
    [switch]$List
)

$ErrorActionPreference = 'Stop'
$smokeRoot = Join-Path $PSScriptRoot 'smoke'

$modules = [ordered]@{
    'entry'     = '退出码、宿主编码自检、启动冒烟靶子'
    'typing'    = '打字与拖拽的真实往返'
    'safety'    = '重试/等待/禁用窗口/dry-run/stdin 等安全语义'
    'features'  = 'window inspect、窗口相对移动、脚本继承目标'
    'loops'     = '脚本循环与"一个进程跑到底"'
    'region'    = '选区浮层：拖拽/单击/ESC 与坐标系回归'
    'record'    = '录制与回放（含 wait-text）'
    'serve'     = '常驻 Runner：CLI 当客户端驱动它'
    'act'       = '--find / click-text / when'
    'flowloops' = '流程里的循环与变量'
    'calls'     = '子流程：内联、vars 与 export'
}

if ($List) {
    Write-Host 'smoke modules:'
    foreach ($name in $modules.Keys) { Write-Host ("  {0,-10} {1}" -f $name, $modules[$name]) }
    exit 0
}

$selected = if ($Only) { @($Only) } else { @($modules.Keys) }
foreach ($name in $selected) {
    if (-not $modules.Contains($name)) {
        Write-Error "unknown smoke module '$name' (see: pwsh tests\smoke.ps1 -List)"
        exit 2
    }
}

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
$selectedLabel = if ($Only) { $selected -join ', ' } else { "全部 $($modules.Count) 个模块" }
Write-Host "smoke testing $Exe  ($selectedLabel)`n"

. (Join-Path $smokeRoot 'common.ps1')

try {
    foreach ($name in $selected) {
        . (Join-Path $smokeRoot "$name.ps1")
    }
}
finally {
    Get-Process KeyMouse.SmokeTarget -ErrorAction SilentlyContinue | Stop-Process -Force
    if ($null -ne $savedClipboard) { Set-Clipboard -Value $savedClipboard }
    Get-Process KeyMouse -ErrorAction SilentlyContinue | Stop-Process -Force
}

Write-Host "`n$script:passed passed, $script:failed failed"
exit ([int]($script:failed -gt 0))