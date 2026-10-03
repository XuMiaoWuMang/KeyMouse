#requires -Version 7
<#
    Builds KeyMouse into a single-file exe at .\dist\KeyMouse.exe

    .\build.ps1                     # framework-dependent (needs .NET runtime)
    .\build.ps1 -SelfContained      # standalone, no runtime needed (~15 MB bigger)
#>
[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    [string]$RuntimeIdentifier = 'win-x64',
    [switch]$SelfContained
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$out = Join-Path $root 'dist'

dotnet publish (Join-Path $root 'src\KeyMouse.Cli\KeyMouse.Cli.csproj') `
    -c $Configuration `
    -r $RuntimeIdentifier `
    --self-contained:$($SelfContained.IsPresent.ToString().ToLowerInvariant()) `
    -p:PublishSingleFile=true `
    --nologo `
    -o $out

if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE" }

Write-Host ''
Write-Host "OK -> $(Join-Path $out 'KeyMouse.exe')" -ForegroundColor Green
