<#
.SYNOPSIS
    发布与撤销发布（GitHub Release）。需要 GITHUB_TOKEN（Contents: write）。

.DESCRIPTION
    没有 token 的时候只能删**标签**（git 走 SSH 就能删），Release 对象删不掉。
    所以这个脚本把两件事都做了：

    -RevokeAll   删掉仓库里所有 Release，然后删本地与远端的旧标签
    -Publish     按当前版本建两个 Release：gui-v<版本>（编辑器 + 安装程序）
                 与 cli-v<版本>（命令行两个单文件）

    版本号从根目录 Directory.Build.props 的 ProductVersion 读，正文由
    tools\release\release-notes.ps1 依 git 历史生成——不再手写发版说明。

    这里**不会**构建任何东西：先跑 tools\build\pack-release.ps1，产物在 artifacts\release\。

.EXAMPLE
    $env:GITHUB_TOKEN = 'ghp_...'
    pwsh tools\release\release.ps1 -Publish -DryRun     # 只打印要做什么
    pwsh tools\release\release.ps1 -Publish
    pwsh tools\release\release.ps1 -RevokeAll
#>
param(
    [switch]$RevokeAll,
    [switch]$Publish,
    [switch]$DryRun,
    [string]$Repository = 'XuMiaoWuMang/KeyMouse'
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$releaseDir = Join-Path $root 'artifacts\release'

[xml]$props = Get-Content (Join-Path $root 'Directory.Build.props') -Raw
$version = @($props.Project.PropertyGroup.ProductVersion | Where-Object { $_ })[0]
if (-not $version) { throw 'Directory.Build.props 里没有 ProductVersion' }

$token = $env:GITHUB_TOKEN
if (-not $token -and -not $DryRun) { throw '需要 GITHUB_TOKEN（Contents: write）' }

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
    if ($DryRun) {
        Write-Host '   （DryRun：只列出会被删掉的）'
        foreach ($release in Get-Releases) { Write-Host ('   - ' + $release.tag_name) }
    }
    else {
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
}

if ($Publish) {
    $guiTag = 'gui-v' + $version
    $cliTag = 'cli-v' + $version

    # 正文由这份脚本按 git 历史生成（它把结果写在标准输出的信息流里，
    # 所以让它落到临时文件再读，而不是管道捕获）。
    $notesFile = Join-Path ([System.IO.Path]::GetTempPath()) ('keymouse-notes-' + $PID + '.md')
    & pwsh -NoProfile -File (Join-Path $root 'tools\release\release-notes.ps1') -Tag $guiTag -Out $notesFile | Out-Null
    $notes = if (Test-Path $notesFile) { Get-Content $notesFile -Raw } else { '## ' + $guiTag }
    Remove-Item $notesFile -Force -ErrorAction SilentlyContinue

    $assets = @{
        $guiTag = @(Get-ChildItem $releaseDir -Filter 'KeyMouseSetup-*.exe' -ErrorAction SilentlyContinue | ForEach-Object { $_.FullName })
        $cliTag = @(
            (Join-Path $releaseDir 'KeyMouse-x64.exe'),
            (Join-Path $releaseDir 'KeyMouse-fx-x64.exe')
        )
    }

    foreach ($tag in @($guiTag, $cliTag)) {
        if ($DryRun) {
            Write-Host ('-> [DryRun] ' + $tag) -ForegroundColor Cyan
            foreach ($file in $assets[$tag]) {
                $state = if (Test-Path $file) { 'ok' } else { '缺文件' }
                Write-Host ('   ' + (Split-Path $file -Leaf) + '  ' + $state)
            }
            continue
        }

        Push-Location $root
        try {
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
        finally { Pop-Location }
    }
}
