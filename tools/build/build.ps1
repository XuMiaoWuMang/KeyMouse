#requires -Version 7
<#
    把 CLI 打包成单文件：

        pwsh tools\build\build.ps1                  # 框架依赖 → artifacts\cli\KeyMouse.exe
        pwsh tools\build\build.ps1 -SelfContained   # 自包含     → artifacts\cli-self\KeyMouse.exe

    日常不要直接用它，用 .\km d（同一个动作，多打一行体积）。这里保留独立入口，
    是因为 CI 与 pack-release.ps1 需要在没有 km 的环境里复现同一条发布命令。
#>
[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    [string]$RuntimeIdentifier = 'win-x64',
    [switch]$SelfContained
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$out = Join-Path $root $(if ($SelfContained) { 'artifacts\cli-self' } else { 'artifacts\cli' })

# 重新发布前先挪走旧产物：单文件打包器（GenerateBundle）遇到上次写了一半的输出，
# 会抛 "It is forbidden to change Manifest state after it was written or BundleId was obtained"，
# 这条错误看着像 SDK 坏了，其实只是残留。没人跑着的旧 exe 删不掉也无所谓——发布本身能覆盖。
if (Test-Path $out) {
    try { Remove-Item -Recurse -Force $out } catch { Write-Host "旧产物删不掉（可能还有进程在用），继续发布" -ForegroundColor Yellow }
}

$dotnetArgs = @(
    'publish', (Join-Path $root 'src\KeyMouse.Cli\KeyMouse.Cli.csproj'),
    '-c', $Configuration,
    '-r', $RuntimeIdentifier,
    ('--self-contained:' + $SelfContained.IsPresent.ToString().ToLowerInvariant()),
    '-p:PublishSingleFile=true',
    '--nologo',
    '-o', $out
)
# 与 .github/workflows/release.yml 对齐：自包含产物压缩，否则本地包会比发布包大一圈。
if ($SelfContained) { $dotnetArgs += '-p:EnableCompressionInSingleFile=true' }

dotnet @dotnetArgs

if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE" }

Write-Host ''
Write-Host "OK -> $(Join-Path $out 'KeyMouse.exe')" -ForegroundColor Green

# OCR 模型跟着命令行走：内置引擎从 <cli 目录>\models\ocr\ 找 det.onnx / rec.onnx，
# 安装包复制 cli 目录时它们自然一起过去。没有模型就不拷——命令行会明确说"回落到外部引擎"。
$ocrSource = Join-Path $root "artifacts\models\ocr"
if (Test-Path (Join-Path $ocrSource "det.onnx")) {
    $ocrTarget = Join-Path $out "models\ocr"
    New-Item -ItemType Directory -Force $ocrTarget | Out-Null
    foreach ($file in @("det.onnx", "rec.onnx")) { Copy-Item (Join-Path $ocrSource $file) $ocrTarget -Force }
    Write-Host "OCR 模型已随包：$ocrTarget" -ForegroundColor DarkGray
} else {
    Write-Host "没有 OCR 模型（tools\build\fetch-ocr-models.ps1），包里的内置引擎会回落到 Tesseract" -ForegroundColor Yellow
}
