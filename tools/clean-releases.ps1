# 删除遗留的 GitHub Release 与标签（例如 gui、v0.1.0 这种早期命名）。
#
# token 从下面两处之一取（前者优先）：
#   $env:GITHUB_TOKEN
#   %USERPROFILE%\.keymouse-github-token   ← 一行纯 token，脚本用完会删掉这个文件
#
#   .\tools\clean-releases.ps1                          # 只列出（不删）
#   .\tools\clean-releases.ps1 -Remove gui,v0.1.0
#   .\tools\clean-releases.ps1 -Remove gui,v0.1.0 -Yes   # 跳过确认

param(
    [string[]]$Remove = @(),
    [switch]$Yes
)

$ErrorActionPreference = 'Stop'
$OutputEncoding = [Console]::OutputEncoding = [Text.Encoding]::UTF8
$repo = 'XuMiaoWuMang/KeyMouse'
$tokenFile = Join-Path $env:USERPROFILE '.keymouse-github-token'

$token = $env:GITHUB_TOKEN
if (-not $token -and (Test-Path $tokenFile)) { $token = (Get-Content $tokenFile -Raw).Trim() }
if (-not $token) { throw "找不到 token：设置 GITHUB_TOKEN 环境变量，或把它写进 $tokenFile" }

$headers = @{
    Authorization = 'Bearer ' + $token
    Accept        = 'application/vnd.github+json'
    'User-Agent'  = 'keymouse-clean'
}
$api = "https://api.github.com/repos/$repo"

Write-Host '=== 全部 Release（含草稿）==='
$releases = Invoke-RestMethod -Uri "$api/releases?per_page=100" -Headers $headers
foreach ($r in $releases) {
    $assets = ($r.assets | ForEach-Object { $_.name }) -join ', '
    Write-Host ("  id={0,-10} tag={1,-16} name={2,-16} draft={3} 资产={4}" -f $r.id, $r.tag_name, $r.name, $r.draft, $assets)
}

Write-Host ''
Write-Host '=== 全部标签 ==='
$tags = Invoke-RestMethod -Uri "$api/tags?per_page=100" -Headers $headers
foreach ($t in $tags) { Write-Host ('  ' + $t.name) }

if ($Remove.Count -eq 0) {
    Write-Host ''
    Write-Host '只列出，没有删除。要删请加 -Remove <名字,名字>（按 tag_name 或 name 精确匹配）。'
    return
}

if (-not $Yes) {
    $answer = Read-Host ("将要删除这些 Release 与标签：" + ($Remove -join ", ") + "。确认？(y/N)")
    if ($answer -notmatch '^[yY]') { Write-Host '已取消。'; return }
}

foreach ($name in $Remove) {
    $matched = $releases | Where-Object { $_.tag_name -eq $name -or $_.name -eq $name }
    if (-not $matched) { Write-Host ("  没有匹配的 Release：" + $name) }
    foreach ($r in $matched) {
        Write-Host ("  删除 Release id=" + $r.id + " tag=" + $r.tag_name)
        Invoke-RestMethod -Uri "$api/releases/$($r.id)" -Method Delete -Headers $headers | Out-Null
    }

    Write-Host ("  删除标签：" + $name)
    $root = Split-Path $PSScriptRoot -Parent
    git -C $root push origin (":refs/tags/" + $name) 2>$null | Out-Null
    git -C $root tag -d $name 2>$null | Out-Null
}

if (Test-Path $tokenFile) {
    Remove-Item $tokenFile -Force
    Write-Host ("  已删除 token 文件：" + $tokenFile)
}
Write-Host '完成。'