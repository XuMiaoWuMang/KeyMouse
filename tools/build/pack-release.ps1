<#
.SYNOPSIS
    打一次发布：编辑器（GUI）+ 命令行 + 安装程序。

.DESCRIPTION
    中间产物与最终产物分开：
      artifacts\editor\          编辑器自包含发布目录（被打成 flowedit.zip）
      artifacts\cli\             命令行，框架依赖单文件（开发树里编辑器用的就是这一个）
      artifacts\cli-self\        命令行，自包含单文件（被打成 cli.zip）
      artifacts\setup\           安装程序发布目录
      artifacts\release\         最终三个文件，发布出去的就是它们

    最终产物：
      KeyMouseSetup-<版本>.exe   安装程序（编辑器 + 需要时才装的命令行）
      KeyMouse-x64.exe           命令行，自包含单文件
      KeyMouse-fx-x64.exe        命令行，框架依赖

    编辑器必须自包含：WinUI 的 Windows App Runtime 在没装的机器上装不起来
    （bootstrapper 对 inbox CBS 运行时返回 0x80670016），所以 269 MB 是这个选择的代价。

    版本号不在这里写死：从根目录 Directory.Build.props 的 ProductVersion 读。

.EXAMPLE
    pwsh tools\build\pack-release.ps1
    pwsh tools\build\pack-release.ps1 -SkipEditor      # 只重打命令行与安装程序
#>
param(
    [switch]$SkipEditor,
    [switch]$SkipCli
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path

function Say([string]$text) { Write-Host $text -ForegroundColor Cyan }

[xml]$props = Get-Content (Join-Path $root 'Directory.Build.props') -Raw
$version = @($props.Project.PropertyGroup.ProductVersion | Where-Object { $_ })[0]
if (-not $version) { throw 'Directory.Build.props 里没有 ProductVersion' }

$artifacts = Join-Path $root 'artifacts'
$editorOut = Join-Path $artifacts 'editor'
$cliOut = Join-Path $artifacts 'cli'
$cliSelfOut = Join-Path $artifacts 'cli-self'
$setupOut = Join-Path $artifacts 'setup'
$release = Join-Path $artifacts 'release'
$assets = Join-Path $root 'src\KeyMouse.Setup\Assets'
New-Item -ItemType Directory -Force -Path $artifacts, $release, $assets | Out-Null

Push-Location $root
try {
    Say "-> 版本 $version"

    if (-not $SkipEditor) {
        Say '-> 发布编辑器（自包含）'
        dotnet publish src\KeyMouse.FlowEditor\KeyMouse.FlowEditor.csproj -c Release -r win-x64 `
            --self-contained true -p:WindowsAppSDKSelfContained=true -o $editorOut --nologo | Out-Null
        if ($LASTEXITCODE -ne 0) { throw '编辑器发布失败' }
    }

    # dotnet publish 不会把 WinUI 的 KeyMouse.FlowEditor.pri 带进输出目录，
    # 缺了它编辑器启动就 XamlParseException（实测）。从同一份 Release 构建里补过来。
    $tfm = 'net10.0-windows10.0.26100.0'
    $built = Join-Path $root ('src\KeyMouse.FlowEditor\bin\Release\' + $tfm + '\win-x64')

    # 根目录瘦身：**不能**把语言目录搬进 language\ ——WinUI 的 MRT 只认 exe 旁边的文化目录，
    # 搬走即崩（实测 0xC000027B / InvalidCastException；对照组：原样与仅换目录都能起来）。
    # 能做的是删掉用不到的语言：界面文案本来就是中文硬编码，框架消息回退英文即可。
    if (Test-Path $editorOut) {
        # 只留中文与英文：界面文案是中文硬编码，其余 96 个语言永远不会被显示，
        # 但每个都有 34–792 KB 的真实资源（实测合计 11.9 MB）。删掉它们不影响启动（已验证）。
        $keep = @('zh-CN', 'en-US', 'en-us')
        $removed = 0
        Get-ChildItem $editorOut -Directory |
            Where-Object { ($_.Name -match '^[a-zA-Z]{2,3}(-[A-Za-z0-9]+)*$') -and ($keep -notcontains $_.Name) } |
            ForEach-Object { Remove-Item $_.FullName -Recurse -Force -ErrorAction SilentlyContinue; $removed++ }
        Say "   删掉用不到的语言目录：$removed 个"

        Get-ChildItem $editorOut -Filter '*.pdb' -File | Remove-Item -Force -ErrorAction SilentlyContinue
        Remove-Item (Join-Path $editorOut 'createdump.exe') -Force -ErrorAction SilentlyContinue
    }

    if (Test-Path $built) {
        Get-ChildItem $built -Filter '*.pri' -File | ForEach-Object {
            Copy-Item $_.FullName $editorOut -Force
        }
        Say '   补上 XAML 资源 KeyMouse.FlowEditor.pri'
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
        @{ From = $editorOut; To = 'flowedit.zip' },
        @{ From = (Join-Path $cliSelfOut 'KeyMouse.exe'); To = 'cli.zip' }
    )) {
        $zip = Join-Path $assets $pair.To
        if (Test-Path $zip) { Remove-Item $zip -Force }
        # cli.zip 只放单个 exe：安装时直接解到 <dir>\cli\KeyMouse.exe
        if ($pair.To -eq 'cli.zip') {
            $stream = [System.IO.File]::Create($zip)
            $archive = [System.IO.Compression.ZipArchive]::new($stream, [System.IO.Compression.ZipArchiveMode]::Create)
            [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
                $archive, $pair.From, 'KeyMouse.exe',
                [System.IO.Compression.CompressionLevel]::SmallestSize) | Out-Null
            $archive.Dispose()
            $stream.Dispose()
        }
        else {
            [System.IO.Compression.ZipFile]::CreateFromDirectory(
                $pair.From, $zip,
                [System.IO.Compression.CompressionLevel]::SmallestSize, $false)
        }
        Say ('   ' + $pair.To + '  ' + [math]::Round((Get-Item $zip).Length/1MB,1) + ' MB')
    }

    Say '-> 构建安装程序'
    dotnet publish src\KeyMouse.Setup\KeyMouse.Setup.csproj -c Release -r win-x64 `
        --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true `
        -o $setupOut --nologo | Out-Null
    if ($LASTEXITCODE -ne 0) { throw '安装程序构建失败' }

    Copy-Item (Join-Path $setupOut 'KeyMouseSetup.exe') `
        (Join-Path $release ('KeyMouseSetup-' + $version + '.exe')) -Force

    Say ''
    Say 'artifacts\release\'
    Get-ChildItem $release -File | Sort-Object Name | ForEach-Object {
        Write-Host ('  {0,-26} {1,7} MB' -f $_.Name, [math]::Round($_.Length/1MB, 1))
    }
}
finally {
    Pop-Location
}
