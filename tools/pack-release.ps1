<#
.SYNOPSIS
    打一次发布：编辑器（GUI）+ 命令行 + 安装程序。

.DESCRIPTION
    产物只有三个，全在 dist\ 里：
      KeyMouseSetup-<版本>.exe   安装程序（编辑器 + 需要时才装的命令行）
      KeyMouse-x64.exe           命令行，自包含单文件
      KeyMouse-fx-x64.exe        命令行，框架依赖

    编辑器必须自包含：WinUI 的 Windows App Runtime 在没装的机器上装不起来
    （bootstrapper 对 inbox CBS 运行时返回 0x80670016），所以 269 MB 是这个选择的代价。

.EXAMPLE
    .\tools\pack-release.ps1
    .\tools\pack-release.ps1 -SkipEditor      # 只重打命令行与安装程序
#>
param(
    [switch]$SkipEditor,
    [switch]$SkipCli
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Push-Location $root
try {
    $version = '0.1.0'
    $assets = Join-Path $root 'tools\KeyMouse.Setup\Assets'
    $dist = Join-Path $root 'dist'
    New-Item -ItemType Directory -Force -Path $assets, $dist | Out-Null

    function Say([string]$text) { Write-Host $text -ForegroundColor Cyan }

    if (-not $SkipEditor) {
        Say '-> 发布编辑器（自包含）'
        dotnet publish editor\KeyMouse.FlowEditor\KeyMouse.FlowEditor.csproj -c Release -r win-x64 `
            --self-contained true -p:WindowsAppSDKSelfContained=true -o out/flowedit --nologo | Out-Null
        if ($LASTEXITCODE -ne 0) { throw '编辑器发布失败' }
    }

    # dotnet publish 不会把 WinUI 的 KeyMouse.FlowEditor.pri 带进输出目录，
    # 缺了它编辑器启动就 XamlParseException（实测）。从同一份 Release 构建里补过来。
    $tfm = 'net10.0-windows10.0.26100.0'
    $built = Join-Path $root ('editor\KeyMouse.FlowEditor\bin\Release\' + $tfm + '\win-x64')
    # 根目录瘦身：**不能**把语言目录搬进 language\ ——WinUI 的 MRT 只认 exe 旁边的文化目录，
    # 搬走即崩（实测 0xC000027B / InvalidCastException；对照组：原样与仅换目录都能起来）。
    # 能做的是删掉用不到的语言：界面文案本来就是中文硬编码，框架消息回退英文即可。
    $publish = Join-Path $root 'out/flowedit'
    if (Test-Path $publish) {
        # 只留中文与英文：界面文案是中文硬编码，其余 96 个语言永远不会被显示，
        # 但每个都有 34–792 KB 的真实资源（实测合计 11.9 MB）。删掉它们不影响启动（已验证）。
        $keep = @('zh-CN', 'en-US', 'en-us')
        $removed = 0
        Get-ChildItem $publish -Directory |
            Where-Object { ($_.Name -match '^[a-zA-Z]{2,3}(-[A-Za-z0-9]+)*$') -and ($keep -notcontains $_.Name) } |
            ForEach-Object { Remove-Item $_.FullName -Recurse -Force -ErrorAction SilentlyContinue; $removed++ }
        Say "   删掉用不到的语言目录：$removed 个"

        Get-ChildItem $publish -Filter '*.pdb' -File | Remove-Item -Force -ErrorAction SilentlyContinue
        Remove-Item (Join-Path $publish 'createdump.exe') -Force -ErrorAction SilentlyContinue
    }

    if (Test-Path $built) {
        Get-ChildItem $built -Filter '*.pri' -File | ForEach-Object {
            Copy-Item $_.FullName (Join-Path $root 'out/flowedit') -Force
        }
        Copy-Item (Join-Path $built 'KeyMouse.FlowEditor.pri') (Join-Path $root 'out/flowedit') -Force
        Say '   补上 XAML 资源 KeyMouse.FlowEditor.pri'
    }

    if (-not $SkipCli) {
        Say '-> 发布命令行（自包含单文件）'
        dotnet publish src\KeyMouse.Cli\KeyMouse.Cli.csproj -c Release -r win-x64 `
            --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true `
            -o out/self --nologo | Out-Null
        if ($LASTEXITCODE -ne 0) { throw '命令行发布失败' }

        Say '-> 发布命令行（框架依赖）'
        dotnet publish src\KeyMouse.Cli\KeyMouse.Cli.csproj -c Release -r win-x64 `
            --self-contained false -p:PublishSingleFile=true -o out/fx --nologo | Out-Null
        if ($LASTEXITCODE -ne 0) { throw '命令行发布失败' }
        Copy-Item out/fx/KeyMouse.exe (Join-Path $dist 'KeyMouse-fx-x64.exe') -Force
        Copy-Item out/self/KeyMouse.exe (Join-Path $dist 'KeyMouse-x64.exe') -Force
        Copy-Item out/fx/KeyMouse.exe (Join-Path $dist 'KeyMouse.exe') -Force
    }

    Say '-> 打包载荷'
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    foreach ($pair in @(
        @{ From = 'out/flowedit'; To = 'flowedit.zip' },
        @{ From = 'out/self';    To = 'cli.zip' }
    )) {
        $zip = Join-Path $assets $pair.To
        if (Test-Path $zip) { Remove-Item $zip -Force }
        # 只认单文件 exe：目录里只有 KeyMouse.exe 一个可执行文件
        if ($pair.To -eq 'cli.zip') {
            Add-Type -AssemblyName System.IO.Compression
            $stream = [System.IO.File]::Create($zip)
            $archive = New-Object System.IO.Compression.ZipArchive($stream, [System.IO.Compression.ZipArchiveMode]::Create)
            [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
                $archive, (Join-Path $root 'out/self/KeyMouse.exe'), 'KeyMouse.exe',
                [System.IO.Compression.CompressionLevel]::SmallestSize) | Out-Null
            $archive.Dispose()
            $stream.Dispose()
        }
        else {
            [System.IO.Compression.ZipFile]::CreateFromDirectory(
                (Join-Path $root $pair.From), $zip,
                [System.IO.Compression.CompressionLevel]::SmallestSize, $false)
        }
        Say ('   ' + $pair.To + '  ' + [math]::Round((Get-Item $zip).Length/1MB,1) + ' MB')
    }

    Say '-> 构建安装程序'
    dotnet publish tools\KeyMouse.Setup\KeyMouse.Setup.csproj -c Release -r win-x64 `
        --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true `
        -o out/setup --nologo | Out-Null
    if ($LASTEXITCODE -ne 0) { throw '安装程序构建失败' }

    $installer = Join-Path $dist ('KeyMouseSetup-' + $version + '.exe')
    Copy-Item out/setup/KeyMouseSetup.exe $installer -Force

    Say ''
    Say 'dist\'
    Get-ChildItem $dist -File | Sort-Object Name | ForEach-Object {
        Write-Host ('  {0,-26} {1,7} MB' -f $_.Name, [math]::Round($_.Length/1MB, 1))
    }
}
finally {
    Pop-Location
}
