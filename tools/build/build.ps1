#requires -Version 7
<#
    把 CLI 打包成单文件：

        pwsh tools\build\build.ps1                  # 框架依赖 → artifacts\cli\KeyMouse.exe
        pwsh tools\build\build.ps1 -SelfContained   # 自包含     → artifacts\cli-self\KeyMouse.exe

    日常不要直接用它，用 .\km d（同一个动作，多打一行体积）。这里保留独立入口，
    是因为 CI 与 pack-release.ps1 需要在没有 km 的环境里复现同一条发布命令。
#>
[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    [string]$RuntimeIdentifier = 'win-x64',
    [switch]$SelfContained
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$out = Join-Path $root $(if ($SelfContained) { 'artifacts\cli-self' } else { 'artifacts\cli' })

$dotnetArgs = @(
    'publish', (Join-Path $root 'src\KeyMouse.Cli\KeyMouse.Cli.csproj'),
    '-c', $Configuration,
    '-r', $RuntimeIdentifier,
    ('--self-contained:' + $SelfContained.IsPresent.ToString().ToLowerInvariant()),
    '-p:PublishSingleFile=true',
    '--nologo',
    '-o', $out
)
# 与 .github/workflows/release.yml 对齐：自包含产物压缩，否则本地包会比发布包大一圈。
if ($SelfContained) { $dotnetArgs += '-p:EnableCompressionInSingleFile=true' }

dotnet @dotnetArgs

if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE" }

Write-Host ''
Write-Host "OK -> $(Join-Path $out 'KeyMouse.exe')" -ForegroundColor Green
