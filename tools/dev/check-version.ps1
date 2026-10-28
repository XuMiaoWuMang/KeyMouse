#requires -Version 7
<#
    版本关卡：版本号只能有一个来源，别处不许再写字面量。

    为什么会有这道关：以前版本散在 6 个地方（两个 csproj、CLI 的 csproj、安装程序常量、
    编辑器标题、安装包文件名），改一个忘一个，结果装出来的标题写着旧版本号、文件名却是新的。
    现在唯一来源是根目录的 Directory.Build.props，其余全部引用 $(ProductVersion) / $(EngineVersion)，
    代码里运行时从程序集读。

    查四件事：
        1. Directory.Build.props 里两条版本线都在，且是 x.y.z
        2. 每个 csproj 用的是 $(ProductVersion) 还是 $(EngineVersion) 符合约定
        3. 任何 csproj / props / .cs 里都没有写死的 x.y.z
        4. 已经打包出来的 artifacts\cli\KeyMouse.exe 自报的版本跟 props 一致（产物旧了也抓得住）

        pwsh tools\dev\check-version.ps1
        pwsh tools\dev\check-version.ps1 -NoArtifact     # 还没打包时只查源码
#>
[CmdletBinding()]
param(
    [string]$Root = (Join-Path $PSScriptRoot '..\..'),
    [switch]$NoArtifact
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path $Root).Path
$problems = [System.Collections.Generic.List[string]]::new()

function Fail([string]$message) { $problems.Add($message); Write-Host "  x $message" -ForegroundColor Red }
function Ok([string]$message) { Write-Host "  ok $message" -ForegroundColor DarkGray }

# ---- 1. 唯一来源 -------------------------------------------------------------------
$propsPath = Join-Path $root 'Directory.Build.props'
if (-not (Test-Path $propsPath)) {
    Write-Host "  x 找不到 $propsPath（版本号的唯一来源）" -ForegroundColor Red
    exit 1
}

[xml]$props = Get-Content $propsPath -Raw
$product = @($props.Project.PropertyGroup.ProductVersion | Where-Object { $_ })[0]
$engine = @($props.Project.PropertyGroup.EngineVersion | Where-Object { $_ })[0]

foreach ($pair in @(@('ProductVersion', $product), @('EngineVersion', $engine))) {
    if (-not $pair[1]) { Fail "Directory.Build.props 缺少 $($pair[0])" }
    elseif ($pair[1] -notmatch '^\d+\.\d+\.\d+$') { Fail "$($pair[0]) 不是 x.y.z 形式：$($pair[1])" }
    else { Ok "$($pair[0]) = $($pair[1])" }
}
if ($problems.Count -gt 0) { Write-Host "`n版本关卡：没过（$($problems.Count) 项）" -ForegroundColor Red; exit 1 }

# ---- 2. 谁该用哪条线 ---------------------------------------------------------------
$expected = @{
    'src\KeyMouse.Cli\KeyMouse.Cli.csproj'               = 'ProductVersion'
    'src\KeyMouse.FlowEditor\KeyMouse.FlowEditor.csproj' = 'ProductVersion'
    'src\KeyMouse.Setup\KeyMouse.Setup.csproj'           = 'ProductVersion'
    'src\KeyMouse.Core\KeyMouse.Core.csproj'             = 'EngineVersion'
    'src\KeyMouse.Runner\KeyMouse.Runner.csproj'         = 'EngineVersion'
}

foreach ($relative in $expected.Keys) {
    $path = Join-Path $root $relative
    if (-not (Test-Path $path)) { Fail "缺项目文件：$relative"; continue }
    $text = Get-Content $path -Raw
    $want = '$(' + $expected[$relative] + ')'
    if ($text -notmatch [regex]::Escape("<Version>$want</Version>")) {
        Fail "$relative 的 <Version> 应当是 $want"
    } else { Ok "$relative 用 $want" }
}

# ---- 3. 不许有内联字面量 ------------------------------------------------------------
$literals = Get-ChildItem -Path (Join-Path $root 'src') -Recurse -File -Include *.csproj,*.props,*.targets |
    Select-String -Pattern '<(ProductVersion|EngineVersion|Version)>\s*\d+\.\d+'

foreach ($hit in $literals) {
    $relative = $hit.Path.Substring($root.Length).TrimStart('\', '/')
    Fail "$relative`:$($hit.LineNumber) 写死了版本号（应引用 `$()` 或删掉）"
}
if (-not $literals) { Ok 'csproj / props 里没有写死的版本号' }

$constants = Get-ChildItem -Path (Join-Path $root 'src') -Recurse -File -Filter *.cs |
    Select-String -Pattern 'const\s+string\s+Version\s*=\s*"\d'

foreach ($hit in $constants) {
    $relative = $hit.Path.Substring($root.Length).TrimStart('\', '/')
    Fail "$relative`:$($hit.LineNumber) 写死了版本常量（运行时从程序集读）"
}
if (-not $constants) { Ok '.cs 里没有写死的版本常量' }

# ---- 4. 产物自报的版本 --------------------------------------------------------------
if (-not $NoArtifact) {
    $exe = Join-Path $root 'artifacts\cli\KeyMouse.exe'
    if (Test-Path $exe) {
        $reported = (& $exe --version 2>&1 | Out-String).Trim()
        if ($reported -notmatch [regex]::Escape($product)) { Fail "artifacts\cli\KeyMouse.exe 自报版本对不上产品版本：$reported" }
        elseif ($reported -notmatch [regex]::Escape($engine)) { Fail "artifacts\cli\KeyMouse.exe 自报版本对不上引擎版本：$reported" }
        else { Ok "产物自报：$reported" }
    } else {
        Write-Host '  - 还没有 artifacts\cli\KeyMouse.exe，跳过产物版本检查' -ForegroundColor DarkGray
    }
}

Write-Host ''
if ($problems.Count -eq 0) {
    Write-Host "版本关卡：通过（产品 $product / 引擎 $engine）" -ForegroundColor Green
    exit 0
}

Write-Host "版本关卡：没过（$($problems.Count) 项）" -ForegroundColor Red
exit 1
