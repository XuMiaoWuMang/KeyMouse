<#
.SYNOPSIS
    打一次发布：编辑器（GUI）+ 命令行 + 安装程序。

.DESCRIPTION
    中间产物与最终产物分开：
      artifacts\editor-ui\      编辑器（Tauri 单 exe，被打成 editorui.zip）
      artifacts\cli\             命令行，框架依赖单文件（开发树里编辑器用的就是这一个）
      artifacts\cli-self\        命令行，自包含单文件（被打成 cli.zip）
      artifacts\setup\           安装程序发布目录
      artifacts\release\         最终三个文件，发布出去的就是它们

    最终产物：
      KeyMouseSetup-<版本>.exe   安装程序（编辑器 + 需要时才装的命令行）
      KeyMouse-x64.exe           命令行，自包含单文件
      KeyMouse-fx-x64.exe        命令行，框架依赖

    编辑器是 Tauri 外壳：一个 exe，界面资源已经编在里面；运行时靠系统的 WebView2，
    安装程序会检查并提示（Installer.WebView2），不把运行时打进来。

    版本号不在这里写死：从根目录 Directory.Build.props 的 ProductVersion 读。

.EXAMPLE
    pwsh tools\build\pack-release.ps1
    pwsh tools\build\pack-release.ps1 -SkipEditor      # 只重打命令行与安装程序
    pwsh tools\build\pack-release.ps1 -Plan            # 只读发布计划
    pwsh tools\build\pack-release.ps1 -UIAccessHelper C:\signed\Input
    # 可选 helper 先验签/manifest，再生成独立 ZIP；不嵌入普通安装器，也不自动安装。
#>
param(
    [switch]$SkipEditor,
    [switch]$SkipCli,
    # Optional separately distributed payload; never embedded in the ordinary installer.
    [string]$UIAccessHelper,
    [switch]$Plan
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path

function Say([string]$text) { Write-Host $text -ForegroundColor Cyan }

[xml]$props = Get-Content (Join-Path $root 'Directory.Build.props') -Raw
$version = @($props.Project.PropertyGroup.ProductVersion | Where-Object { $_ })[0]
if (-not $version) { throw 'Directory.Build.props 里没有 ProductVersion' }

$artifacts = Join-Path $root 'artifacts'
$editorUiOut = Join-Path $artifacts 'editor-ui'
$cliOut = Join-Path $artifacts 'cli'
$cliSelfOut = Join-Path $artifacts 'cli-self'
$setupOut = Join-Path $artifacts 'setup'
$release = Join-Path $artifacts 'release'
$assets = Join-Path $root 'src\KeyMouse.Setup\Assets'
$uiAccessPlan = $null
if ($UIAccessHelper) {
    # Fail before builds or writes; includes Authenticode Valid + embedded UIAccess manifest checks.
    $uiAccessPlan = & (Join-Path $PSScriptRoot 'install-uiaccess.ps1') -HelperPath $UIAccessHelper -Mode Plan
}
if ($Plan) {
    [pscustomobject]@{
        Version = $version; ReleaseDirectory = $release
        BuildEditor = -not $SkipEditor; BuildCli = -not $SkipCli
        UIAccess = $uiAccessPlan
        UIAccessEmbeddedInInstaller = $false
    }
    return
}
New-Item -ItemType Directory -Force -Path $artifacts, $release, $assets | Out-Null

Push-Location $root
try {
    Say "-> 版本 $version"

    if (-not $SkipEditor) {
        # Tauri 外壳：一个 exe，界面已经编在里面，不需要 .pri、语言目录、bootstrapper。
        # 它要的是系统的 WebView2 运行时，安装程序会检查并提示（见 Installer.WebView2）。
        Say '-> 发布编辑器（Tauri release）'
        Push-Location 'src\KeyMouse.EditorUI'
        try {
            $env:CARGO_HTTP_CHECK_REVOKE = 'false'
            $env:CARGO_NET_RETRY = '5'
            $env:CARGO_HTTP_TIMEOUT = '120'
            pnpm tauri build --no-bundle 2>&1 | Out-Null
            if ($LASTEXITCODE -ne 0) { throw '新壳构建失败（先跑一次 pnpm install）' }
        }
        finally {
            Pop-Location
        }

        if (Test-Path $editorUiOut) { Remove-Item $editorUiOut -Recurse -Force }
        New-Item -ItemType Directory -Force -Path $editorUiOut | Out-Null
        Copy-Item 'src\KeyMouse.EditorUI\src-tauri\target\release\KeyMouse.Editor.exe' $editorUiOut -Force
    }

    if (-not $SkipCli) {
        Say '-> 发布命令行（自包含单文件）'
        dotnet publish src\KeyMouse.Cli\KeyMouse.Cli.csproj -c Release -r win-x64 `
            --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true `
            -o $cliSelfOut --nologo | Out-Null
        if ($LASTEXITCODE -ne 0) { throw '命令行发布失败' }

        # artifacts\cli 是开发树里编辑器实际调用的那一个；发布版复用它，保证"测的就是发的"。
        Say '-> 发布命令行（框架依赖单文件）'
        dotnet publish src\KeyMouse.Cli\KeyMouse.Cli.csproj -c Release -r win-x64 `
            --self-contained false -p:PublishSingleFile=true -o $cliOut --nologo | Out-Null
        if ($LASTEXITCODE -ne 0) { throw '命令行发布失败' }
    }

    Copy-Item (Join-Path $cliOut 'KeyMouse.exe') (Join-Path $release 'KeyMouse-fx-x64.exe') -Force
    Copy-Item (Join-Path $cliSelfOut 'KeyMouse.exe') (Join-Path $release 'KeyMouse-x64.exe') -Force

    Say '-> 打包安装载荷'
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    foreach ($pair in @(
        @{ From = (Join-Path $editorUiOut 'KeyMouse.Editor.exe'); Name = 'KeyMouse.Editor.exe'; To = 'editorui.zip' },
        @{ From = (Join-Path $cliSelfOut 'KeyMouse.exe'); Name = 'KeyMouse.exe'; To = 'cli.zip' }
    )) {
        $zip = Join-Path $assets $pair.To
        if (Test-Path $zip) { Remove-Item $zip -Force }

        # 两个载荷都是单文件：editorui → <安装目录>\KeyMouse.Editor.exe；
        # cli → <安装目录>\cli\KeyMouse.exe，另外带上内置 OCR 模型。
        $stream = [System.IO.File]::Create($zip)
        $archive = [System.IO.Compression.ZipArchive]::new($stream, [System.IO.Compression.ZipArchiveMode]::Create)
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
            $archive, $pair.From, $pair.Name,
            [System.IO.Compression.CompressionLevel]::SmallestSize) | Out-Null

        if ($pair.To -eq 'cli.zip') {
            # 内置引擎（paddle-ocr）要那两份 onnx；不带的话装完会静默回落到 Tesseract。
            $modelDir = Join-Path $artifacts 'models\ocr'
            foreach ($model in @('det.onnx', 'rec.onnx')) {
                $file = Join-Path $modelDir $model
                if (-not (Test-Path $file)) {
                    Write-Warning ("没有 $file —— 安装版会用 Tesseract 兜底；先跑 tools\build\fetch-ocr-models.ps1")
                    continue
                }
                [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
                    $archive, $file, "models/ocr/$model",
                    [System.IO.Compression.CompressionLevel]::SmallestSize) | Out-Null
            }
        }

        $archive.Dispose()
        $stream.Dispose()
        Say ('   ' + $pair.To + '  ' + [math]::Round((Get-Item $zip).Length/1MB,1) + ' MB')
    }

    Say '-> 构建安装程序'
    dotnet publish src\KeyMouse.Setup\KeyMouse.Setup.csproj -c Release -r win-x64 `
        --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true `
        -o $setupOut --nologo | Out-Null
    if ($LASTEXITCODE -ne 0) { throw '安装程序构建失败' }

    Copy-Item (Join-Path $setupOut 'KeyMouseSetup.exe') `
        (Join-Path $release ('KeyMouseSetup-' + $version + '.exe')) -Force

    # A separate, explicit administrator action installs UIAccess. Ordinary setup stays unprivileged.
    # Refuse stale optional payloads on subsequent non-UIAccess releases.
    $uiAccessZip = Join-Path $release ('KeyMouse-UIAccess-' + $version + '.zip')
    if (Test-Path -LiteralPath $uiAccessZip) { Remove-Item -LiteralPath $uiAccessZip -Force }
    if ($UIAccessHelper) {
        $staging = Join-Path $artifacts ('uiaccess-package-' + [guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path $staging | Out-Null
        $copiedPayload = Join-Path $staging 'Input'
        New-Item -ItemType Directory -Path $copiedPayload | Out-Null
        foreach ($file in $uiAccessPlan.Files) {
            Copy-Item -LiteralPath $file.Path -Destination (Join-Path $copiedPayload $file.Name)
        }
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'install-uiaccess.ps1') -Destination $staging
        $copiedPlan = & (Join-Path $PSScriptRoot 'install-uiaccess.ps1') -HelperPath $copiedPayload -Mode Plan
        foreach ($file in $uiAccessPlan.Files) {
            if ($file.Sha256 -ne ($copiedPlan.Files | Where-Object Name -CEQ $file.Name).Sha256) {
                throw ('UIAccess payload changed during packaging: ' + $file.Name)
            }
        }
        [IO.Compression.ZipFile]::CreateFromDirectory($staging, $uiAccessZip)
        Say ('-> 可选 UIAccess 独立载荷：' + $uiAccessZip)
    }

    Say ''
    Say 'artifacts\release\'
    Get-ChildItem $release -File | Sort-Object Name | ForEach-Object {
        Write-Host ('  {0,-26} {1,7} MB' -f $_.Name, [math]::Round($_.Length/1MB, 1))
    }
}
finally {
    Pop-Location
}
