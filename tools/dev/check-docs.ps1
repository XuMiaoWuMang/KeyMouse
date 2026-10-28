#requires -Version 7
<#
    文档关卡：仓库里每一条相对链接都必须指向真实存在的文件。

    为什么值得单独一道关：文档是分开写的（README、docs/、docs/guide/、docs/tools/），
    一次改名就会让另一个人几天后点进 404，而没人会回头重读。这个检查不需要桌面、
    不需要构建，所以 CI 每次 push 都跑。

    跳过这些目录，它们不是文档：
        bin obj artifacts .git .vs evidence
    evidence 是本地取证归档：里面的 summary.md 会复述 OCR 读到的东西，而 OCR 读到的
    文本长得跟 markdown 链接一模一样（实测：样例里的「[是](3)[取消]」被当成指向 3 的断链）。

        pwsh tools\dev\check-docs.ps1
#>
[CmdletBinding()]
param([string]$Root = (Join-Path $PSScriptRoot '..\..'))

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path $Root).Path

$files = Get-ChildItem -Path $root -Filter *.md -Recurse -File |
    Where-Object { $_.FullName -notmatch '\\(bin|obj|artifacts|\.git|\.vs|evidence)\\' }

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
