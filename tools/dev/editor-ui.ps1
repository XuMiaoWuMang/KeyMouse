#requires -Version 7
<#
.SYNOPSIS
    新编辑器外壳（src\KeyMouse.EditorUI）的日常命令：装依赖、起开发窗口、打包前端、类型检查。

.DESCRIPTION
    Tauri 的第一次构建有三处环境坑，这个脚本把它们收在一处，免得每人踩一遍：

      1. npm 源直连不稳        → 装依赖时走 npmmirror（--Registry 可改）
      2. cargo 的镜像源校验证书吊销列表失败（CRYPT_E_REVOCATION_OFFLINE）
                               → 编译前把 CARGO_HTTP_CHECK_REVOKE 置 false
      3. vite 的 watcher 会去盯 Rust 的 target/ 并把 dev 服务器带崩
                               → vite.config.ts 里已经排除了 src-tauri

    日志落在 artifacts\tests\editor-ui\，一次一行，不覆盖。

.PARAMETER Verb
    setup    只装依赖（pnpm install）
    dev      起开发窗口（vite + cargo run）
    build    只打前端产物（pnpm build）
    check    svelte-check 类型与模板检查
    icons    从 src-tauri\icons\source.png 重新生成整套图标
    clean    删 node_modules 与 src-tauri\target

.PARAMETER Registry
    装依赖用的 npm 源，默认 https://registry.npmmirror.com

.EXAMPLE
    pwsh tools\dev\editor-ui.ps1 dev
    pwsh tools\dev\editor-ui.ps1 setup -Registry https://registry.npmjs.org
#>
[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [ValidateSet('setup', 'dev', 'build', 'check', 'icons', 'clean')]
    [string]$Verb = 'dev',

    [string]$Registry = 'https://registry.npmmirror.com'
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$app = Join-Path $root 'src\KeyMouse.EditorUI'
$logs = Join-Path $root 'artifacts\tests\editor-ui'

if (-not (Test-Path $app)) {
    throw "找不到 $app —— 这个脚本只管新外壳；老编辑器请用 km.ps1 的 e 动词。"
}

New-Item -ItemType Directory -Force -Path $logs | Out-Null

function Invoke-Step {
    param([string]$Name, [scriptblock]$Body)
    $stamp = Get-Date -Format 'HH:mm:ss'
    Write-Host ("[{0}] {1}" -f $stamp, $Name) -ForegroundColor Cyan
    $started = Get-Date
    & $Body
    if ($LASTEXITCODE -ne 0 -and $null -ne $LASTEXITCODE) {
        throw "$Name 失败（退出码 $LASTEXITCODE）"
    }
    $elapsed = [int]((Get-Date) - $started).TotalSeconds
    Write-Host ("[{0}] {1} 完成，{2}s" -f (Get-Date -Format 'HH:mm:ss'), $Name, $elapsed) -ForegroundColor DarkGray
}

Push-Location $app
try {
    switch ($Verb) {
        'setup' {
            Invoke-Step '装依赖（pnpm install）' {
                pnpm install --registry $Registry --fetch-retries 5 --fetch-retry-maxtimeout 120000
            }
        }

        'dev' {
            # cargo 走的是本机 ~/.cargo/config.toml 里的镜像；不关吊销检查会一直 SSL 报错。
            $env:CARGO_HTTP_CHECK_REVOKE = 'false'
            $env:CARGO_NET_RETRY = '5'
            $env:CARGO_HTTP_TIMEOUT = '120'
            $log = Join-Path $logs ("dev-{0}.log" -f (Get-Date -Format 'yyyyMMdd-HHmmss'))
            Write-Host "开发窗口：日志写到 $log" -ForegroundColor DarkGray
            Write-Host "关掉窗口后 Ctrl+C 结束；只改前端时不需要重启这个命令。" -ForegroundColor DarkGray
            pnpm tauri dev 2>&1 | Tee-Object -FilePath $log
        }

        'build' {
            Invoke-Step '打前端产物（pnpm build）' { pnpm build }
            $dist = Join-Path $app 'dist'
            Get-ChildItem $dist -Recurse -File |
                Measure-Object -Property Length -Sum |
                ForEach-Object { Write-Host ("dist/ 共 {0} 个文件，{1:N1} KB" -f $_.Count, ($_.Sum / 1KB)) -ForegroundColor DarkGray }
        }

        'check' {
            Invoke-Step '类型与模板检查（svelte-check）' { pnpm check }
        }

        'icons' {
            # 源图由 tools\build\make-icon.py 生成；这里只负责从源图铺开一整套尺寸。
            $source = Join-Path $app 'src-tauri\icons\source.png'
            if (-not (Test-Path $source)) {
                throw "没有 $source —— 先跑 python tools\build\make-icon.py 生成源图（需要 Pillow）。"
            }
            Invoke-Step '生成图标（tauri icon）' { pnpm tauri icon $source }
            # Tauri 会顺手生成 android/ios 两套，这个产品不发移动端，删掉免得占地方。
            foreach ($extra in 'android', 'ios') {
                $dir = Join-Path $app "src-tauri\icons\$extra"
                if (Test-Path $dir) { Remove-Item -Recurse -Force $dir }
            }
        }

        'clean' {
            foreach ($dir in (Join-Path $app 'node_modules'), (Join-Path $app 'src-tauri\target'), (Join-Path $app 'dist')) {
                if (Test-Path $dir) {
                    Write-Host "删 $dir" -ForegroundColor DarkGray
                    Remove-Item -Recurse -Force $dir
                }
            }
        }
    }
}
finally {
    Pop-Location
}
