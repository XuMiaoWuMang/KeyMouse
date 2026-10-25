#requires -Version 7
<#
    km —— 日常开发入口。一条命令做一件事，默认只做该做的那件。

        .\km b            只编译 CLI（含 Core / Runner），不碰编辑器        ~1s
        .\km ba           编译整个解决方案（含 WinUI 编辑器）              ~16s
        .\km e            编译编辑器 **并重新打包 dist**（编辑器连的是它）  ~18s
        .\km e -NoDist    只编译编辑器                                    ~13s
        .\km t            单元测试（全部，不需要桌面，跑已编译的 exe）      ~1s
        .\km t parsing    只跑改到的模块（可写多个）                        ~1s
        .\km tl           列出测试模块
        .\km t -n flow    不重新编译，直接跑                                ~0.2s
        .\km s            桌面冒烟（需要解锁的交互式桌面）
        .\km v            四道关卡全跑（构建 / 单测 / 文档 / 打包 / 冒烟）
        .\km d            打包单文件到 dist\KeyMouse.exe                   ~5s
        .\km d -s         打包自包含单文件（体积大，不需要运行时）
        .\km r key type hi    运行刚编译出来的 KeyMouse，参数原样转发
        .\km r -n --help      不重新编译，直接运行
        .\km w            监视 src/ 与 tests/：改了就跟一次「编译 + 单元测试」
        .\km w -once      只跟一次（用来确认监视本身是好的）
        .\km docs         文档链接检查
        .\km clean        清掉 bin / obj / dist / out（含根目录那三个）

    为什么默认只编译 CLI：整个解决方案里 13s 花在 WinUI 编辑器的 XAML 上，
    而 CLI 只改 Core 时重建是 1.7s。编辑器不参与就不该付这笔钱。

    为什么 km e 顺带打包：编辑器启动的引擎是 dist\KeyMouse.exe（编辑器目录 → 仓库 dist
    → PATH 依次找），只重建 bin 的话，在编辑器里跑的还是旧逻辑——实测踩过：换行输入
    明明改了，编辑器里跑出来仍然是一行。这 5 秒买的是"看到的就是刚改的"。
#>
[CmdletBinding()]
param(
    [Parameter(Position = 0)][string]$Verb = 'help',
    [Parameter(ValueFromRemainingArguments = $true)][string[]]$Rest = @()
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$cli = Join-Path $root 'src\KeyMouse.Cli\KeyMouse.Cli.csproj'
$cliExe = Join-Path $root 'src\KeyMouse.Cli\bin\Release\net10.0-windows\KeyMouse.exe'
$tests = Join-Path $root 'tests\KeyMouse.Tests\KeyMouse.Tests.csproj'
$testsExe = Join-Path $root 'tests\KeyMouse.Tests\bin\Release\net10.0-windows\KeyMouse.Tests.exe'
$editor = Join-Path $root 'editor\KeyMouse.FlowEditor'
$dist = Join-Path $root 'dist'

function Say([string]$text, [string]$color = 'Cyan') { Write-Host $text -ForegroundColor $color }

function Elapsed($sw) { [string]::Format('{0:N1}s', $sw.Elapsed.TotalSeconds) }

function Timed([string]$label, [scriptblock]$body) {
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    & $body
    $code = $LASTEXITCODE
    $sw.Stop()
    if ($code -ne 0) {
        Say ("  x " + $label + " 失败（退出码 " + $code + "，" + (Elapsed $sw) + "）") 'Red'
        exit $code
    }
    Say ("  ok " + $label + "  " + (Elapsed $sw)) 'DarkGray'
}

function RunDotnet([string[]]$DotnetArgs) { & dotnet @DotnetArgs }

function BuildCli {
    if ($Rest -contains '-q') { return }
    Say '-> 编译 CLI（Core + Runner）'
    Timed 'cli' { RunDotnet @('build', $cli, '-c', 'Release', '--nologo', '-v:q') }
}

function BuildTests {
    Say '-> 编译单元测试'
    Timed 'tests' { RunDotnet @('build', $tests, '-c', 'Release', '--nologo', '-v:q') }
}

function TestCycle() {
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    RunDotnet @('build', $cli, '-c', 'Release', '--nologo', '-v:q') | Out-Null
    if ($LASTEXITCODE -ne 0) { Say '  x 编译失败' 'Red'; return }
    RunDotnet @('build', $tests, '-c', 'Release', '--nologo', '-v:q') | Out-Null
    if ($LASTEXITCODE -ne 0) { Say '  x 测试项目编译失败' 'Red'; return }

    $out = & $testsExe 2>&1 | Out-String
    $code = $LASTEXITCODE
    $sw.Stop()
    if ($code -ne 0) {
        Say ("  x 单元测试没过（" + (Elapsed $sw) + "）") 'Red'
        Write-Host $out
    } else {
        $line = ($out -split "\r?\n" | Where-Object { $_ -match '\S' } | Select-Object -Last 1)
        Say ("  ok " + $line + "  (" + (Elapsed $sw) + ")") 'DarkGray'
    }
}

function Get-Stamp {
    $files = Get-ChildItem -Path (Join-Path $root 'src'), (Join-Path $root 'tests') -Recurse -File -ErrorAction SilentlyContinue |
        Where-Object { $_.Extension -in '.cs', '.json' }
    if (-not $files) { return 0 }
    return ($files | Measure-Object -Property LastWriteTimeUtc -Maximum).Maximum.Ticks
}

switch ($Verb) {
    'b' { BuildCli; break }
    'build' { BuildCli; break }

    { $_ -in 'ba', 'build-all' } {
        Say '-> 编译整个解决方案（含编辑器）'
        Timed 'sln' { RunDotnet @('build', (Join-Path $root 'KeyMouse.sln'), '-c', 'Release', '--nologo', '-v:q') }
        break
    }

    { $_ -in 'e', 'editor' } {
        Say '-> 编译编辑器（WinUI 3）'
        Timed 'editor' { RunDotnet @('build', $editor, '-c', 'Release', '--nologo', '-v:q') }

        # 编辑器连的是 dist\KeyMouse.exe：只重建 bin 会让它在旧引擎上跑
        if ($Rest -notcontains '-NoDist') {
            Say '-> 重新打包 dist（编辑器用的就是这个 exe）'
            Timed 'publish' {
                RunDotnet @('publish', $cli, '-c', 'Release', '-r', 'win-x64', '--self-contained:false',
                            '-p:PublishSingleFile=true', '--nologo', '-v:q', '-o', $dist)
            }
        }
        break
    }

    { $_ -in 't', 'test' } {
        if ($Rest -contains 'bench') {
            Say '-> 性能基准'
            if (-not (Test-Path $testsExe)) { BuildTests }
            & $testsExe bench
            exit $LASTEXITCODE
        }

        $modules = @()
        $pass = @()
        $noBuild = $false
        foreach ($a in $Rest) {
            if ($a -in '-n', '--no-build') { $noBuild = $true }
            elseif ($a -in '-l', '--list') { $pass += '--list' }
            elseif ($a.StartsWith('-')) { $pass += $a }
            else { $modules += $a }
        }

        if ($Rest -contains '-l' -or $Rest -contains '--list') {
            if (-not (Test-Path $testsExe)) { BuildTests }
            & $testsExe --list
            exit $LASTEXITCODE
        }

        $stale = (-not (Test-Path $testsExe)) -or (-not (Test-Path $cliExe))
        if ($stale -or -not $noBuild) { BuildTests }

        $targs = @()
        if ($modules.Count -gt 0) { $targs += '--only'; $targs += ($modules -join ',') }
        $targs += $pass
        & $testsExe @targs
        exit $LASTEXITCODE
    }

    { $_ -in 'tl', 'test-list' } {
        if (-not (Test-Path $testsExe)) { BuildTests }
        & $testsExe --list
        exit $LASTEXITCODE
    }

    { $_ -in 's', 'smoke' } {
        if (-not (Test-Path $cliExe)) { BuildCli }
        Say '-> 桌面冒烟（需要解锁的交互式桌面）'
        & pwsh -NoProfile -File (Join-Path $root 'tests\smoke.ps1') -Exe $cliExe @Rest
        exit $LASTEXITCODE
    }

    { $_ -in 'v', 'verify' } {
        Say '-> 四道关卡（verify.ps1）'
        & pwsh -NoProfile -File (Join-Path $root 'verify.ps1') @Rest
        exit $LASTEXITCODE
    }

    'docs' {
        Say '-> 文档链接检查'
        & pwsh -NoProfile -File (Join-Path $root 'tests\check-docs.ps1')
        exit $LASTEXITCODE
    }

    { $_ -in 'd', 'dist' } {
        $selfContained = $Rest -contains '-s' -or $Rest -contains '--self-contained'
        $dq = @('publish', $cli, '-c', 'Release', '-r', 'win-x64',
                ('--self-contained:' + $selfContained.ToString().ToLowerInvariant()),
                '-p:PublishSingleFile=true', '--nologo', '-o', $dist)
        if ($selfContained) { $dq += '-p:EnableCompressionInSingleFile=true' }
        Say ('-> 打包' + $(if ($selfContained) { '（自包含，压缩）' } else { '（框架依赖）' }))
        Timed 'publish' { RunDotnet $dq }
        $exe = Join-Path $dist 'KeyMouse.exe'
        if (Test-Path $exe) {
            $mb = [math]::Round((Get-Item $exe).Length / 1MB, 1)
            Say ('   ' + $exe + '  ' + $mb + ' MB') 'Green'
        }
        break
    }

    { $_ -in 'r', 'run' } {
        $noBuild = $Rest -contains '-n' -or $Rest -contains '--no-build'
        $forward = @($Rest | Where-Object { $_ -notin '-n', '--no-build' })
        if (-not $noBuild) { BuildCli }
        elseif (-not (Test-Path $cliExe)) { BuildCli }
        & $cliExe @forward
        exit $LASTEXITCODE
    }

    { $_ -in 'w', 'watch' } {
        $once = $Rest -contains '-once' -or $Rest -contains '--once'
        Say '-> 监视 src/ 与 tests/（Ctrl+C 结束）' 'Cyan'
        $stamp = Get-Stamp
        TestCycle
        if ($once) { break }
        while ($true) {
            Start-Sleep -Milliseconds 400
            $now = Get-Stamp
            if ($now -eq $stamp) { continue }
            $stamp = $now
            Start-Sleep -Milliseconds 250
            Say '-> 检测到改动' 'Cyan'
            TestCycle
        }
        break
    }

    { $_ -in 'c', 'clean' } {
        Say '-> 清理 bin / obj / dist / out'
        Get-ChildItem -Path (Join-Path $root 'src'), (Join-Path $root 'tests'), $editor -Recurse -Directory -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -in 'bin', 'obj' } |
            ForEach-Object { Remove-Item -Recurse -Force $_.FullName -ErrorAction SilentlyContinue }

        # 根目录这几个是 SDK 建临时项目、以及发布暂存留下的，和项目无关但很占地方
        foreach ($name in 'bin', 'obj', 'out') {
            $path = Join-Path $root $name
            if (Test-Path $path) { Remove-Item -Recurse -Force $path -ErrorAction SilentlyContinue }
        }

        if (Test-Path $dist) { Remove-Item -Recurse -Force $dist }
        Say '   done' 'DarkGray'
        break
    }

    default {
        $inside = $false
        foreach ($line in (Get-Content $PSCommandPath)) {
            if ($line -match '^\s*<#\s*$') { $inside = $true; continue }
            if ($line -match '^\s*#>\s*$') { break }
            if ($inside -and $line -match '\S') { Write-Host $line }
        }
        break
    }
}
