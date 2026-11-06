# 取 OCR 模型（PaddleOCR PP-OCRv4 的 det / rec，ONNX）
#
# 为什么不在仓库里放模型：15 MB 的二进制只在换模型时才变，塞进 git 之后每次 clone 都得拖，
# 而且历史里会留一串没人能读的 blob。它们由这个脚本取一次，落在 artifacts\models\ocr\。
#
# 模型来源：PyPI 上的 rapidocr-onnxruntime（Apache-2.0），它把 PaddleOCR 官方导出的
# ch_PP-OCRv4_det_infer / ch_PP-OCRv4_rec_infer 直接打在 wheel 里；字符表也内嵌在
# rec.onnx 的 metadata（`character` 键）里，所以不需要单独下 ppocr_keys_v1.txt。
#
# 用法：
#   pwsh tools\build\fetch-ocr-models.ps1            # 有就跳过
#   pwsh tools\build\fetch-ocr-models.ps1 -Force     # 重下
#   pwsh tools\build\fetch-ocr-models.ps1 -Verify    # 只校验现有文件
#
# 装好之后 `KeyMouse probe --engine builtin` 与 `read-text` 步骤都走这一套；
# 没装就回落到 Tesseract（`--engine tesseract`）。

[CmdletBinding()]
param(
    [switch]$Force,
    [switch]$Verify,
    [string]$WheelUrl = 'https://pypi.tuna.tsinghua.edu.cn/simple/rapidocr-onnxruntime/'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$target = Join-Path $root 'artifacts\models\ocr'
$det = Join-Path $target 'det.onnx'
$rec = Join-Path $target 'rec.onnx'

# 这两个哈希钉住的是 PP-OCRv4 mobile det/rec（中文 + 英文）。换模型就换哈希，别默默放行。
$expected = @{
    'det.onnx' = 'D2A7720D45A54257208B1E13E36A8479894CB74155A5EFE29462512D42F49DA9'
    'rec.onnx' = '48FC40F24F6D2A207A2B1091D3437EB3CC3EB6B676DC3EF9C37384005483683B'
}

function Test-Models {
    foreach ($pair in $expected.GetEnumerator()) {
        $path = Join-Path $target $pair.Key
        if (-not (Test-Path $path)) { return "缺 $($pair.Key)" }
        $actual = (Get-FileHash $path -Algorithm SHA256).Hash
        if ($actual -ne $pair.Value) { return "$($pair.Key) 哈希不符（$($actual.Substring(0,12))…）" }
    }
    return $null
}

if ($Verify) {
    $problem = Test-Models
    if ($problem) { Write-Host "OCR 模型不完整：$problem" -ForegroundColor Yellow; exit 1 }
    Write-Host "OCR 模型就位：$target" -ForegroundColor Green
    exit 0
}

if (-not $Force) {
    $problem = Test-Models
    if (-not $problem) {
        Write-Host "OCR 模型已就位（$target），跳过" -ForegroundColor DarkGray
        exit 0
    }
}

New-Item -ItemType Directory -Force $target | Out-Null
$temp = Join-Path ([System.IO.Path]::GetTempPath()) ("keymouse-ocr-" + [Guid]::NewGuid().ToString('n').Substring(0, 8))
New-Item -ItemType Directory -Force $temp | Out-Null

try {
    Write-Host '找 wheel…' -ForegroundColor DarkGray
    $index = (Invoke-WebRequest -Uri $WheelUrl -TimeoutSec 60 -Headers @{ 'User-Agent' = 'keymouse' }).Content
    $match = [regex]::Match($index, 'href="([^"]*rapidocr_onnxruntime-[0-9.]+-py3-none-any\.whl[^"]*)"')
    if (-not $match.Success) { throw "索引页里没有 wheel：$WheelUrl" }

    $url = $match.Groups[1].Value
    if ($url -notmatch '^https?://') {
        # 索引里的相对链接是 ../../packages/…，和索引页同源。
        $base = [Uri]$WheelUrl
        $url = [Uri]::new($base, $url).AbsoluteUri
    }

    $wheel = Join-Path $temp 'rapidocr.whl'
    Write-Host "下 wheel（约 15 MB）：$url" -ForegroundColor DarkGray
    Invoke-WebRequest -Uri $url -OutFile $wheel -TimeoutSec 300 -Headers @{ 'User-Agent' = 'keymouse' }

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = [System.IO.Compression.ZipFile]::OpenRead($wheel)
    try {
        $wanted = @{
            'ch_PP-OCRv4_det_infer.onnx' = $det
            'ch_PP-OCRv4_rec_infer.onnx' = $rec
        }
        foreach ($entry in $zip.Entries) {
            $name = [System.IO.Path]::GetFileName($entry.FullName)
            if ($wanted.ContainsKey($name)) {
                [System.IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $wanted[$name], $true)
                Write-Host "  $name -> $(Split-Path -Leaf $wanted[$name])" -ForegroundColor DarkGray
            }
        }
    }
    finally { $zip.Dispose() }

    $problem = Test-Models
    if ($problem) { throw "校验没过：$problem" }
    foreach ($file in Get-ChildItem $target -Filter *.onnx) {
        Write-Host ("  {0}  {1:N1} KB  sha256 {2}…" -f $file.Name, ($file.Length / 1KB), (Get-FileHash $file.FullName -Algorithm SHA256).Hash.Substring(0, 12))
    }
    Write-Host "OCR 模型就位：$target" -ForegroundColor Green
}
finally {
    Remove-Item -Recurse -Force $temp -ErrorAction SilentlyContinue
}
