#requires -Version 7
<#
    km —— 日常开发入口。一条命令做一件事，默认只做该做的那件。

        .\km b            只编译 CLI（含 Core / Runner），不碰编辑器        ~1s
        .\km ba           编译整个解决方案（含 WinUI 编辑器与安装程序）     ~18s
        .\km e            编译编辑器，并把 CLI 发到 artifacts\cli（编辑器连它）
        .\km e -NoCli     只编译编辑器，不碰 artifacts
        .\km e -NoElevate 普通权限构建编辑器（调界面用：不弹 UAC，也能被普通权限的 KeyMouse 点）
        .\km u            起新编辑器外壳（Tauri + Svelte）的开发模式
        .\km ub           打新外壳的发布版
        .\km ut           跑新外壳的两层测试（vitest + cargo test）
        .\km t            单元测试（全部，不需要桌面，跑已编译的 exe）      ~1s
        .\km t parsing    只跑改到的模块（可写多个）                        ~1s
        .\km tl           列出测试模块
        .\km t -n flow    不重新编译，直接跑                                ~0.2s
        .\km s            桌面冒烟（需要解锁的交互式桌面）
        .\km v            全部关卡（构建 / 单测 / 文档 / 版本 / 打包 / 冒烟）
        .\km d            打包单文件到 artifacts\cli\KeyMouse.exe          ~5s
        .\km d -s         打包自包含单文件（体积大，不需要运行时）
        .\km r key type hi    运行刚编译出来的 KeyMouse，参数原样转发
        .\km r -n --help      不重新编译，直接运行
        .\km w            监视 src/ 与 tests/：改了就跟一次「编译 + 单元测试」
        .\km w -once      只跟一次（用来确认监视本身是好的）
        .\km env          打印环境判定：开发树还是安装版、两边的命令行在哪
        .\km docs         文档链接检查
        .\km clean        清掉 bin / obj / artifacts

    为什么默认只编译 CLI：整个解决方案里大部分时间花在 WinUI 编辑器的 XAML 上，
    而 CLI 只改 Core 时重建是 1.7s。编辑器不参与就不该付这笔钱。

    为什么 km e 顺带打包 CLI：开发树里编辑器找的是 artifacts\cli\KeyMouse.exe
    （安装版找的是自己目录下的 cli\KeyMouse.exe，两者约定见 docs/guide/environments.md）。
    只重建 bin 的话，在编辑器里跑的还是旧逻辑——实测踩过：换行输入明明改了，
    编辑器里跑出来仍然是一行。这 5 秒买的是"看到的就是刚改的"。
#>
[CmdletBinding()]
param(
    [Parameter(Position = 0)][string]$Verb = 'help',
    [Parameter(ValueFromRemainingArguments = $true)][string[]]$Rest = @()
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$artifacts = Join-Path $root 'artifacts'
$cliProject = Join-Path $root 'src\KeyMouse.Cli\KeyMouse.Cli.csproj'
$cliBin = Join-Path $root 'src\KeyMouse.Cli\bin\Release\net10.0-windows\KeyMouse.exe'
$cliArtifact = Join-Path $artifacts 'cli\KeyMouse.exe'
$testsProject = Join-Path $root 'tests\KeyMouse.Tests\KeyMouse.Tests.csproj'
$testsExe = Join-Path $root 'tests\KeyMouse.Tests\bin\Release\net10.0-windows\KeyMouse.Tests.exe'
$editorProject = Join-Path $root 'src\KeyMouse.FlowEditor\KeyMouse.FlowEditor.csproj'
$solution = Join-Path $root 'KeyMouse.sln'

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
    Timed 'cli' { RunDotnet @('build', $cliProject, '-c', 'Release', '--nologo', '-v:q') }
}

function BuildTests {
    Say '-> 编译单元测试'
    Timed 'tests' { RunDotnet @('build', $testsProject, '-c', 'Release', '--nologo', '-v:q') }
}

function PublishCli([switch]$SelfContained) {
    $dir = Join-Path $artifacts $(if ($SelfContained) { 'cli-self' } else { 'cli' })
    $dq = @('publish', $cliProject, '-c', 'Release', '-r', 'win-x64',
            ('--self-contained:' + $SelfContained.IsPresent.ToString().ToLowerInvariant()),
            '-p:PublishSingleFile=true', '--nologo', '-v:q', '-o', $dir)
    if ($SelfContained) { $dq += '-p:EnableCompressionInSingleFile=true' }
    Timed 'publish' { RunDotnet $dq }
    $exe = Join-Path $dir 'KeyMouse.exe'
    if (Test-Path $exe) {
        $mb = [math]::Round((Get-Item $exe).Length / 1MB, 1)
        Say ('   ' + $exe + '  ' + $mb + ' MB') 'Green'
    }
}

function TestCycle {
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    RunDotnet @('build', $cliProject, '-c', 'Release', '--nologo', '-v:q') | Out-Null
    if ($LASTEXITCODE -ne 0) { Say '  x 编译失败' 'Red'; return }
    RunDotnet @('build', $testsProject, '-c', 'Release', '--nologo', '-v:q') | Out-Null
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
        Say '-> 编译整个解决方案（编辑器 + 安装程序）'
        Timed 'sln' { RunDotnet @('build', $solution, '-c', 'Release', '--nologo', '-v:q') }
        break
    }

    { $_ -in 'e', 'editor' } {
        Say '-> 编译编辑器（WinUI 3）'
        # -NoElevate：换成 app.unelevated.manifest，出来的编辑器是普通权限。
        # 只是调界面时用它——省掉每次启动的 UAC，也让普通权限的 KeyMouse 能点它。
        # 清单是链接进 apphost 的，而 Windows App SDK 会把我们给的清单**再合并一次**到
        # obj\…\Manifests\app.manifest；这两个产物都不会因为 -p:KeyMouseElevated 变了就重做，
        # 于是会出现"改了参数、exe 里还是上一轮的权限"。先删掉它们再编。
        $editorObj = Join-Path $root 'src\KeyMouse.FlowEditor\obj'
        if (Test-Path $editorObj) {
            Get-ChildItem $editorObj -Recurse -Filter 'app.manifest' -File -ErrorAction SilentlyContinue |
                ForEach-Object { Remove-Item $_.FullName -Force -ErrorAction SilentlyContinue }
        }
        $editorExe = Join-Path $root 'src\KeyMouse.FlowEditor\bin\Release\net10.0-windows10.0.26100.0\win-x64\KeyMouse.FlowEditor.exe'
        if (Test-Path $editorExe) {
            try { Remove-Item $editorExe -Force }
            catch { Say '   编辑器还开着（KeyMouse.FlowEditor.exe 被占用），先关掉它' }
        }
        $editorArgs = @('build', $editorProject, '-c', 'Release', '--nologo', '-v:q')
        if ($Rest -contains '-NoElevate') {
            Say '   普通权限构建（app.unelevated.manifest）：录不到管理员窗口'
            $editorArgs += '-p:KeyMouseElevated=false'
        }
        Timed 'editor' { RunDotnet $editorArgs }

        # 开发树里编辑器找的是 artifacts\cli\KeyMouse.exe：只重建 bin 会让它在旧引擎上跑
        if ($Rest -notcontains '-NoCli') {
            Say '-> 重新打包 artifacts\cli（开发树的编辑器用的就是这个 exe）'
            PublishCli
        }
        break
    }

    { $_ -in 'u', 'ui' } {
        # 新外壳（Tauri）。真正的逻辑在 tools\dev\editor-ui.ps1——那里还要处理 cargo 的
        # 证书吊销、vite 的 watch 忽略等环境坑，不适合搬进 km。
        Say '-> 新编辑器外壳（Tauri）'
        & pwsh -NoProfile -File (Join-Path $PSScriptRoot 'editor-ui.ps1') dev
        exit $LASTEXITCODE
    }

    { $_ -in 'ub', 'ui-build' } {
        Say '-> 新编辑器外壳：打包（pnpm build + cargo build --release）'
        & pwsh -NoProfile -File (Join-Path $PSScriptRoot 'editor-ui.ps1') build
        exit $LASTEXITCODE
    }

    { $_ -in 'ut', 'ui-test' } {
        Say '-> 新编辑器外壳：两层测试（vitest + cargo test）'
        & pwsh -NoProfile -File (Join-Path $PSScriptRoot 'editor-ui.ps1') test
        exit $LASTEXITCODE
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

        $stale = (-not (Test-Path $testsExe)) -or (-not (Test-Path $cliBin))
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
        if (-not (Test-Path $cliBin)) { BuildCli }
        Say '-> 桌面冒烟（需要解锁的交互式桌面；每次跑进 artifacts\tests\ 下新目录）'
        & pwsh -NoProfile -File (Join-Path $root 'tests\smoke\run.ps1') -Exe $cliBin @Rest
        exit $LASTEXITCODE
    }

    { $_ -in 'v', 'verify' } {
        Say '-> 全部关卡（tools\dev\verify.ps1）'
        & pwsh -NoProfile -File (Join-Path $root 'tools\dev\verify.ps1') @Rest
        exit $LASTEXITCODE
    }

    { $_ -in 'env', 'environment' } {
        if (-not (Test-Path $cliArtifact)) {
            Say '-> artifacts\cli 还没有，先打包'
            PublishCli
        }
        & $cliArtifact doctor @Rest
        exit $LASTEXITCODE
    }

    'docs' {
        Say '-> 文档链接检查'
        & pwsh -NoProfile -File (Join-Path $root 'tools\dev\check-docs.ps1')
        exit $LASTEXITCODE
    }

    { $_ -in 'd', 'dist' } {
        $selfContained = $Rest -contains '-s' -or $Rest -contains '--self-contained'
        Say ('-> 打包到 artifacts\' + $(if ($selfContained) { 'cli-self（自包含，压缩）' } else { 'cli（框架依赖）' }))
        PublishCli -SelfContained:$selfContained
        break
    }

    { $_ -in 'r', 'run' } {
        $noBuild = $Rest -contains '-n' -or $Rest -contains '--no-build'
        $forward = @($Rest | Where-Object { $_ -notin '-n', '--no-build' })
        if (-not $noBuild) { BuildCli }
        elseif (-not (Test-Path $cliBin)) { BuildCli }
        & $cliBin @forward
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
        Say '-> 清理 bin / obj / artifacts'
        Get-ChildItem -Path (Join-Path $root 'src'), (Join-Path $root 'tests') -Recurse -Directory -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -in 'bin', 'obj' } |
            ForEach-Object { Remove-Item -Recurse -Force $_.FullName -ErrorAction SilentlyContinue }

        if (Test-Path $artifacts) { Remove-Item -Recurse -Force $artifacts }
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
