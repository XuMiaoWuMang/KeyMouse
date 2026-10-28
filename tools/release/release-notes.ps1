# 把两次发布之间的提交压成一份精简清单，写进 Release 正文。
#
#   pwsh tools\release\release-notes.ps1                            # 预览（对比上一个同族标签）
#   pwsh tools\release\release-notes.ps1 -Tag cli-v0.2.1 -Out notes.md   # 指定标签与输出文件
#   pwsh tools\release\release-notes.ps1 -Range "cli-v0.2.0..HEAD"       # 手动指定区间
#
# CI 在推标签时调用它，把结果作为 softprops/action-gh-release 的 body_path。

param(
    [string]$Tag = '',
    [string]$Out = '',
    [string]$Range = '',
    [int]$Max = 60
)

$ErrorActionPreference = 'Stop'
$OutputEncoding = [Console]::OutputEncoding = [Text.Encoding]::UTF8
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path

if (-not $Tag) { $Tag = (& git -C $repoRoot describe --tags --abbrev=0 2>$null) }
if (-not $Tag) { $Tag = 'unknown' }

# 同一条发布线只跟自己的上一个标签比：cli-* 与 gui-* 各自独立。
$family = if ($Tag -like 'cli-*') { 'cli' } elseif ($Tag -like 'gui-*') { 'gui' } else { '' }

if (-not $Range) {
    $previous = ''
    if ($family) {
        $previous = & git -C $repoRoot tag -l ($family + '-v*') --sort=-v:refname | Where-Object { $_ -ne $Tag } | Select-Object -First 1
    }

    # 同族还没有上一个标签（第一次发这条线）：退到任何上一个标签；
    # 连那个都没有时只取最近若干提交——清单是给人看的，不是全量档案。
    if (-not $previous) {
        $previous = & git -C $repoRoot tag -l --sort=-v:refname | Where-Object { $_ -ne $Tag } | Select-Object -First 1
    }

    # 关键：cli-* 与 gui-* 往往指向同一个提交（一次改动同时发两条线）。
    # 那种"上一个标签"和 HEAD 是同一个提交，区间算出来是空的——那就等同于没有上一个标签。
    if ($previous) {
        $headsha = (& git -C $repoRoot rev-list -n1 HEAD)
        $prevsha = (& git -C $repoRoot rev-list -n1 $previous)
        if ($headsha -eq $prevsha) { $previous = '' }
    }
    if ($previous) {
        $Range = $previous + '..HEAD'
    } else {
        # 没有任何标签可比（浅克隆、或第一次发布）：退回最近若干提交，绝不产出空清单。
        $count = [int](& git -C $repoRoot rev-list --count HEAD)
        $back = [Math]::Min(25, [Math]::Max(0, $count - 1))
        $Range = if ($back -le 0) { 'HEAD' } else { 'HEAD~' + $back + '..HEAD' }
    }
}

$commits = & git -C $repoRoot log --no-merges --pretty=format:'%h|%s' $Range 2>$null
if (-not $commits) { $commits = @() } else { $commits = @($commits) }

$labels = @{
    feat = '新增'; fix = '修复'; perf = '优化'; refactor = '重构'; build = '构建'; docs = '文档'; chore = '其它'; test = '测试'
}

$items = New-Object System.Collections.Generic.List[string]
foreach ($line in $commits) {
    $parts2 = $line -split '\|', 2
    if ($parts2.Count -lt 2) { continue }
    $sha = $parts2[0]; $subject = $parts2[1]

    # 只保留对用户有意义的类型；纯版本号、合并、以及空提交不进清单。
    if ($subject -match '^(\w+)(\(([^)]*)\))?!?:\s*(.+)$') {
        $type = $Matches[1]; $scope = $Matches[3]; $text = $Matches[4]
        if (-not $labels.ContainsKey($type)) { continue }
        $prefix = $labels[$type]
        $where = if ($scope) { "（$scope）" } else { '' }
        $items.Add("- **$prefix**$where：$text  ``$sha``")
    } else {
        $items.Add("- $subject  ``$sha``")
    }
}

if ($items.Count -eq 0) { $items.Add("- 本次只有内部整理，没有面向用户的变更。") }

$total = $items.Count
$more = ''
if ($items.Count -gt $Max) {
    $more = "> 其余 " + ($items.Count - $Max) + " 项为同类改动，未逐条列出（见提交历史）。"
    $items = $items[0..($Max - 1)]
}

$lines = New-Object System.Collections.Generic.List[string]
$lines.Add('## 本次更新（' + $total + ' 项）')
$lines.Add('')
foreach ($item in $items) { $lines.Add($item) }
if ($more) { $lines.Add($more) }
$lines.Add('')
$lines.Add('---')
$lines.Add('')
$lines.Add('安装：下载下方的 KeyMouseSetup-*.exe（编辑器，自包含）或 KeyMouse-x64.exe（命令行，自带运行时）。')
$lines.Add('')
$lines.Add('区间：`' + $Range + '`')

$text = ($lines -join "`n")
if ($Out) {
    $path = if ([IO.Path]::IsPathRooted($Out)) { $Out } else { Join-Path $repoRoot $Out }
    Set-Content -Path $path -Value $text -Encoding UTF8
    Write-Host "已写出 $path（$($items.Count) 项）"
} else {
    Write-Host $text
}