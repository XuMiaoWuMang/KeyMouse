#requires -Version 7
<#
    文档关卡：检查相对链接、流程步骤清单以及循环的默认上限。

    为什么值得单独一道关：文档是分开写的（README、docs/、docs/guide/、docs/tools/），
    一次改名就会让另一个人几天后点进 404，而没人会回头重读。这个检查不需要桌面、
    不需要构建，所以 CI 每次 push 都跑。

    跳过这些目录，它们不是文档：
        bin obj artifacts .git .vs evidence node_modules target
    evidence 是本地取证归档：里面的 summary.md 会复述 OCR 读到的东西，而 OCR 读到的
    文本长得跟 markdown 链接一模一样（实测：样例里的「[是](3)[取消]」被当成指向 3 的断链）。

        pwsh tools\dev\check-docs.ps1
#>
[CmdletBinding()]
param([string]$Root = (Join-Path $PSScriptRoot '..\..'))

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path $Root).Path

# Only public candidates in a checkout; private notes and build trees are not documentation inputs.
if (Test-Path -LiteralPath (Join-Path $root '.git')) {
    $names = git -C $root -c core.quotepath=false ls-files --cached --others --exclude-standard -- '*.md'
    if ($LASTEXITCODE -ne 0) { throw 'Could not enumerate Git documentation candidates' }
    $files = @(foreach ($name in $names) {
        $path = Join-Path $root $name
        if (Test-Path -LiteralPath $path -PathType Leaf) { Get-Item -LiteralPath $path }
    })
} else {
    # Also support an exported source archive (no Git metadata).
    $files = Get-ChildItem -Path $root -Filter *.md -Recurse -File |
        Where-Object { $_.FullName -notmatch '[/\\](bin|obj|artifacts|\.git|\.vs|evidence|node_modules|target)[/\\]' }
}

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

# Only read the step table, not examples elsewhere on the page: merely mentioning a
# step in a JSON snippet does not document its interface.
$schema = Get-Content -Raw -LiteralPath (Join-Path $root 'src/KeyMouse.Core.Flow.Validation/flow.schema.json') | ConvertFrom-Json -AsHashtable
$interface = Get-Content -Raw -LiteralPath (Join-Path $root 'docs/reference/interface.md')
$table = [regex]::Match($interface, '(?m)^\| 步骤 \|[^\n]*\n(?:^\|[^\n]*\n)+').Value
$rows = @{}
foreach ($row in $table -split '\r?\n') {
    if ($row -notmatch '^\| (?<steps>`[^|]+) \|') { continue }
    foreach ($step in [regex]::Matches($Matches.steps, '`([\w-]+)`')) { $rows[$step.Groups[1].Value] = $row }
}
$drift = 0
foreach ($step in $schema.steps.Keys) {
    if (!$rows.ContainsKey($step)) { Write-Host "  undocumented step: $step"; $drift++; continue }
    foreach ($field in $schema.steps[$step].fields | Where-Object name -eq maxRounds) {
        $expected = '`maxRounds`（默认 ' + $field.default + '）'
        if (!$rows[$step].Contains($expected)) { Write-Host "  ${step}: expected $expected"; $drift++ }
    }
}
Write-Host "checked $($schema.steps.Count) documented step(s); mismatches: $drift"
exit ([int]($broken -gt 0 -or $drift -gt 0))
