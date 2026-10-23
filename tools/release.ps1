<#
.SYNOPSIS
    发布与撤销发布（GitHub Release）。需要 GITHUB_TOKEN（Contents: write）。

.DESCRIPTION
    没有 token 的时候只能删**标签**（git 走 SSH 就能删），Release 对象删不掉。
    所以这个脚本把两件事都做了：

    -RevokeAll   删掉仓库里所有 Release，然后删本地与远端的旧标签
    -Publish     按 0.1.0 建两个 Release：gui（编辑器 + 安装程序）与 v0.1.0（命令行）

.EXAMPLE
    $env:GITHUB_TOKEN = 'ghp_...'
    .\tools\release.ps1 -RevokeAll
    .\tools\release.ps1 -Publish
#>
param(
    [switch]$RevokeAll,
    [switch]$Publish,
    [string]$Repository = 'XuMiaoWuMang/KeyMouse'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$token = $env:GITHUB_TOKEN
if (-not $token) { throw '需要 GITHUB_TOKEN（Contents: write）' }

$headers = @{
    Authorization          = 'Bearer ' + $token
    Accept                 = 'application/vnd.github+json'
    'X-GitHub-Api-Version' = '2022-11-28'
    'User-Agent'           = 'KeyMouse-release'
}
$api = 'https://api.github.com/repos/' + $Repository

function Get-Releases {
    $all = @()
    for ($page = 1; $page -le 10; $page++) {
        $chunk = Invoke-RestMethod -Uri ($api + '/releases?per_page=100&page=' + $page) -Headers $headers
        if (-not $chunk) { break }
        $all += $chunk
        if ($chunk.Count -lt 100) { break }
    }
    return $all
}

if ($RevokeAll) {
    Write-Host '-> 撤销所有 Release' -ForegroundColor Cyan
    foreach ($release in Get-Releases) {
        Invoke-RestMethod -Method Delete -Uri ($api + '/releases/' + $release.id) -Headers $headers | Out-Null
        Write-Host ('   - ' + $release.tag_name + ' ' + $release.name)
    }

    Push-Location $root
    try {
        $tags = @(git tag)
        foreach ($tag in $tags) {
            git tag -d $tag | Out-Null
            Write-Host ('   删标签（本地）' + $tag)
        }
        if ($tags.Count -gt 0) {
            git push origin --delete @tags 2>&1 | Out-Null
            Write-Host ('   删标签（远端）' + ($tags -join ', '))
        }
    }
    finally { Pop-Location }
}

if ($Publish) {
    Push-Location $root
    try {
        $notes = @'
## 0.1.0

首个以 **0.1.0** 起算的版本。之前的版本已全部撤销。

- **KeyMouse 流程编辑器（gui）**：一个安装程序，装完即用（编辑器自包含，不需要先装 .NET）。
  安装时若没检测到 KeyMouse 命令行，会一并装到安装目录下的 cli 子目录。
- **KeyMouse（v0.1.0）**：命令行工具。自包含单文件约 49 MB，或框架依赖约 0.5 MB。
- 界面与文档当前**只有中文**。
'@

        $assets = @{
            'gui'    = @(Get-ChildItem (Join-Path $root 'dist') -Filter 'KeyMouseSetup-*.exe' | ForEach-Object { $_.FullName })
            'v0.1.0' = @(
                (Join-Path $root 'dist\KeyMouse-x64.exe'),
                (Join-Path $root 'dist\KeyMouse-fx-x64.exe')
            )
        }

        foreach ($tag in @('gui', 'v0.1.0')) {
            $body = @{ tag_name = $tag; name = $tag; body = $notes; draft = $false; prerelease = $false } | ConvertTo-Json
            $release = Invoke-RestMethod -Method Post -Uri ($api + '/releases') -Headers $headers -Body $body -ContentType 'application/json'
            foreach ($file in $assets[$tag]) {
                if (-not (Test-Path $file)) { Write-Host ('   缺文件，跳过：' + $file) -ForegroundColor Yellow; continue }
                $name = [uri]::EscapeDataString((Split-Path $file -Leaf))
                $upload = 'https://uploads.github.com/repos/' + $Repository + '/releases/' + $release.id + '/assets?name=' + $name
                Invoke-RestMethod -Method Post -Uri $upload -Headers $headers -InFile $file -ContentType 'application/octet-stream' | Out-Null
                Write-Host ('   + ' + $tag + ' : ' + (Split-Path $file -Leaf))
            }
        }
    }
    finally { Pop-Location }
}
