#requires -Version 7
<#
    Checks that every relative link in the repository's markdown resolves.

    The docs are split across README.md and docs/*.md, so a rename breaks links in
    files nobody re-reads. This needs no desktop and no build, so CI runs it on
    every push.

        pwsh tests/check-docs.ps1
#>
[CmdletBinding()]
param([string]$Root = (Join-Path $PSScriptRoot '..'))

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path $Root).Path

$files = Get-ChildItem -Path $root -Filter *.md -Recurse -File |
    Where-Object { $_.FullName -notmatch '\\(bin|obj|dist|out|\.git|\.vs)\\' }

$checked = 0
$broken = 0

foreach ($file in $files) {
    $relative = $file.FullName.Substring($root.Length).TrimStart('\', '/')
    $text = Get-Content -LiteralPath $file.FullName -Raw

    foreach ($match in [regex]::Matches($text, '\]\(([^)\s]+)\)')) {
        $link = $match.Groups[1].Value
        if ($link -match '^[a-z][a-z0-9+.-]*:' -or $link.StartsWith('#') -or $link.StartsWith('//')) { continue }

        $path = $link.Split('#')[0]
        if ([string]::IsNullOrWhiteSpace($path)) { continue }

        $checked++
        $target = [System.IO.Path]::GetFullPath((Join-Path $file.DirectoryName $path))
        if (-not (Test-Path -LiteralPath $target)) {
            $broken++
            Write-Host "  broken: $relative -> $link"
        }
    }
}

Write-Host "checked $checked relative link(s) across $($files.Count) markdown file(s); broken: $broken"
exit ([int]($broken -gt 0))
