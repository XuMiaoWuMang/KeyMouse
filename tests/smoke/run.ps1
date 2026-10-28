#requires -Version 7
<#
    桌面冒烟测试：真的开一个窗口，让 KeyMouse 往里打字，再从剪贴板读回来对账。
    需要有人登着的交互式桌面（锁屏跑不了，见 tools\dev\verify.ps1 的判定）。

    每次运行都开一个新的运行目录，互不覆盖：

        artifacts\tests\smoke-20261029-143012\
            run.log            完整输出（含每个模块的耗时）
            summary.md         人读的结论
            summary.json       机读的结论
            tmp\               临时文件：模块自己的 .txt/.json，以及 %TEMP% 的重定向
            各种临时脚本      模块自己的 .txt / .json 都落在这里

        pwsh tests\smoke\run.ps1                       # 全部模块（发布前跑这个）
        pwsh tests\smoke\run.ps1 -Only loops,flowloops # 只跑改到的模块（平时用这个，快）
        pwsh tests\smoke\run.ps1 -List                 # 看有哪些模块
        pwsh tests\smoke\run.ps1 -Exe path\to\KeyMouse.exe

    模块在 tests\smoke\modules\，公共部分在 tests\smoke\lib\common.ps1。
    模块按列表顺序执行：`entry` 负责启动冒烟靶子，后面的模块都往它里面打字。
    不会碰你开着的任何程序，结束时还原剪贴板。

    跑之前会先验一次"这个会话到底收不收得到注入的输入"（tools\dev\check-input.ps1）。
    收不到就直接停（退出码 3）并说明原因：那种环境下每个断言都会红，红得毫无信息量。
    已知会话正常、只想省掉那两秒时用 -SkipInputCheck。

    退出码：0 = 全部通过；1 = 有断言失败；2 = 模块名写错；3 = 本会话收不到注入的输入。
#>
[CmdletBinding()]
param(
    [string]$Exe,
    [string]$TargetExe,
    [string[]]$Only,
    [switch]$List,
    [switch]$SkipInputCheck
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
if (-not $Exe) { $Exe = Join-Path $root 'artifacts\cli\KeyMouse.exe' }
if (-not $TargetExe) { $TargetExe = Join-Path $root 'tests\KeyMouse.SmokeTarget\bin\Release\net10.0-windows\KeyMouse.SmokeTarget.exe' }

$modules = [ordered]@{
    'entry'     = '退出码、宿主编码自检、启动冒烟靶子'
    'typing'    = '打字与拖拽的真实往返'
    'safety'    = '重试/等待/禁用窗口/dry-run/stdin 等安全语义'
    'features'  = 'window inspect、窗口相对移动、脚本继承目标'
    'focus'     = '聚焦与还原：最小化窗口的同意语义、抢前台的实测'
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
        # 不用 Write-Error：$ErrorActionPreference = 'Stop' 会把它变成终止错误，
        # 退出码就成了 1，和"有模块失败"混在一起。
        Write-Host "unknown smoke module '$name' (see: pwsh tests\smoke\run.ps1 -List)" -ForegroundColor Red
        exit 2
    }
}

if (-not (Test-Path $Exe)) { Write-Error "找不到 KeyMouse.exe：$Exe（先跑 pwsh tools\dev\km.ps1 b 或 tools\build\build.ps1）"; exit 1 }
if (-not (Test-Path $TargetExe)) { Write-Error "找不到冒烟靶子：$TargetExe（先跑 dotnet build KeyMouse.sln -c Release）"; exit 1 }
$Exe = (Resolve-Path $Exe).Path
$TargetExe = (Resolve-Path $TargetExe).Path

# ---- 前置：本会话收不收得到注入的输入 ------------------------------------------------
# 不做这一步的话，一个收不到输入的会话会把几十条断言一次性染红，而所有红点的根因只有一个。
if (-not $SkipInputCheck) {
    & pwsh -NoProfile -File (Join-Path $root 'tools\dev\check-input.ps1') -Exe $Exe -TargetExe $TargetExe | Out-Host
    if ($LASTEXITCODE -eq 3) {
        Write-Host ''
        Write-Host '冒烟测试没跑：这个会话收不到 KeyMouse 注入的输入，跑了也只是几十条假红。' -ForegroundColor Yellow
        Write-Host '换一个真正交互的终端再跑；确认会话没问题时可以加 -SkipInputCheck 跳过这次自检。'
        exit 3
    }
    if ($LASTEXITCODE -ne 0) {
        Write-Host "输入自检没做成（exit=$LASTEXITCODE），先把它修好。" -ForegroundColor Red
        exit $LASTEXITCODE
    }
}

# ---- 本次运行的新目录 --------------------------------------------------------------
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$runDir = Join-Path $root ('artifacts\tests\smoke-' + $stamp)
New-Item -ItemType Directory -Force -Path $runDir | Out-Null

# 临时目录也搬进运行目录：probe 的中间位图、录制草稿、Runner 的管道线索都留在这一次运行里，
# 跑完随目录一起删。顺带绕开一种环境问题——某些受限环境里 %TEMP% 对子进程不可写，
# 那时 probe 会报 "Access to the path ... is denied"，看着像产品坏了，其实只是临时目录。
$runTemp = Join-Path $runDir 'tmp'
New-Item -ItemType Directory -Force -Path $runTemp | Out-Null
$savedTemp = @{ TEMP = $env:TEMP; TMP = $env:TMP }
$env:TEMP = $runTemp
$env:TMP = $runTemp

# 每个模块里的临时文件都落在运行目录里（common.ps1 会把它导出成 $RunDir）。
$script:RunDir = $runDir
$script:passed = 0
$script:failed = 0
$script:results = [System.Collections.Generic.List[object]]::new()

function Check([string]$what, [bool]$ok, [string]$detail = '') {
    if ($ok) { $script:passed++; Write-Host "  ok   $what" }
    else { $script:failed++; Write-Host "  FAIL $what  -> $detail" }
}

$selectedLabel = if ($Only) { $selected -join ', ' } else { "全部 $($modules.Count) 个模块" }
Write-Host "smoke testing $Exe"
Write-Host "  ($selectedLabel)"
Write-Host "  run dir: $runDir`n"

# 只留最近 10 次：取证靠最近几次，攒着只会把盘吃满。
Get-ChildItem (Join-Path $root 'artifacts\tests') -Directory -Filter 'smoke-*' -ErrorAction SilentlyContinue |
    Sort-Object Name -Descending | Select-Object -Skip 10 |
    ForEach-Object { Remove-Item $_.FullName -Recurse -Force -ErrorAction SilentlyContinue }

$transcript = Join-Path $runDir 'run.log'
Start-Transcript -Path $transcript -Force | Out-Null

Push-Location $runDir   # 模块里的相对路径都落在这里
try {
    . (Join-Path $PSScriptRoot 'lib\common.ps1')

    try {
        foreach ($name in $selected) {
            $before = $script:failed
            $started = Get-Date
            Write-Host "`n--- module: $name ---"
            # 每个模块都从"靶子可见且在前台"开始：上一个模块可能为了断言把它最小化过。
            if ($name -ne 'entry' -and -not (Reset-Target)) {
                Write-Host '  (靶子没拉回前台，本模块的结果仅供参考)' -ForegroundColor DarkYellow
            }
            # 一个模块崩了不该带走后面的模块：记一笔，接着跑。
            try {
                . (Join-Path $PSScriptRoot "modules\$name.ps1")
            }
            catch {
                Check "$name 模块异常中断" $false $_.Exception.Message
                Write-Host ("  " + $_.ScriptStackTrace) -ForegroundColor DarkGray
            }
            $script:results.Add([pscustomobject]@{
                module   = $name
                seconds  = [math]::Round(((Get-Date) - $started).TotalSeconds, 1)
                # 夹住下限：模块内直接改计数（或异常路径）时不该出现负数，那只会让人怀疑统计而不是产品。
                failures = [math]::Max(0, $script:failed - $before)
            })
        }
    }
    finally {
        Get-Process KeyMouse.SmokeTarget -ErrorAction SilentlyContinue | Stop-Process -Force
        if ($null -ne $savedClipboard) { Set-Clipboard -Value $savedClipboard }
        Get-Process KeyMouse -ErrorAction SilentlyContinue | Stop-Process -Force
    }
}
finally {
    Stop-Transcript | Out-Null
    Pop-Location
    $env:TEMP = $savedTemp.TEMP
    $env:TMP = $savedTemp.TMP
}

# ---- 结论：两个文件，一个给人看，一个给脚本看 --------------------------------------
$summaryJson = Join-Path $runDir 'summary.json'
[pscustomobject]@{
    started  = $stamp
    exe      = $Exe
    modules  = $script:results
    passed   = $script:passed
    failed   = $script:failed
} | ConvertTo-Json -Depth 4 | Set-Content $summaryJson -Encoding UTF8

$lines = [System.Collections.Generic.List[string]]::new()
$lines.Add('# smoke ' + $stamp)
$lines.Add('')
$lines.Add("- KeyMouse: ``$Exe``")
$lines.Add('- 通过：' + $script:passed + '，失败：' + $script:failed)
$lines.Add('')
$lines.Add('| 模块 | 耗时 | 失败 |')
$lines.Add('| --- | --- | --- |')
foreach ($r in $script:results) {
    $lines.Add('| ' + $r.module + ' | ' + $r.seconds + 's | ' + $r.failures + ' |')
}
Set-Content (Join-Path $runDir 'summary.md') -Value ($lines -join "`n") -Encoding UTF8

Write-Host "`n$script:passed passed, $script:failed failed"
Write-Host "运行记录：$runDir"
exit ([int]($script:failed -gt 0))
