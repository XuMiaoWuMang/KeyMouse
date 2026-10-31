#requires -Version 7
<#
.SYNOPSIS
把本地提交推到远端，并在推不动的时候说清楚"卡在哪一层"。

.DESCRIPTION
推不上去有三种完全不同的原因，报错却常常长得差不多：

  1. 网络到不了 GitHub（这台机器上见过：22 端口超时、443 也不通）—— 换网络或挂代理；
  2. 身份不对（没权限、密钥没加载）—— `ssh -T git@github.com` 能看出是谁；
  3. 远端有新提交（non-fast-forward）—— 先拉再推，别强推。

这个脚本把三件事按顺序查一遍，只在第一种情况下给出"过一会儿再来"的结论，
不会建议你 reset 或者 force。

.PARAMETER Remote
远端名，默认 origin。

.PARAMETER Branch
分支名，默认当前分支。

.PARAMETER Timeout
单次连接超时秒数，默认 12。别调太大：网络不通时等一分钟只会更难受。

.EXAMPLE
pwsh tools\dev\push.ps1
推 main 到 origin，成功就一句话，失败就说明卡在哪。

.NOTES
退出码：0 已推送（或本来就没有要推的）；3 网络到不了远端；4 身份或权限问题；5 远端有新提交。
#>
[CmdletBinding()]
param(
    [string]$Remote = 'origin',
    [string]$Branch = '',
    [int]$Timeout = 12
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
Set-Location $root

function Say([string]$text, [string]$colour = 'Gray') {
    Write-Host $text -ForegroundColor $colour
}

if (-not $Branch) {
    $Branch = (& git rev-parse --abbrev-ref HEAD).Trim()
    if ($LASTEXITCODE -ne 0) { throw '读不出当前分支' }
}

$remoteUrl = (& git remote get-url $Remote 2>$null)
if (-not $remoteUrl) { throw "远端 $Remote 不存在" }
Say "远端 $Remote = $remoteUrl，分支 $Branch"

& git fetch --quiet $Remote 2>$null
$ahead = (& git rev-list --count "$Remote/$Branch..$Branch" 2>$null)
if (-not $ahead) {
    Say "远端还没有 $Branch 分支，这次推送会新建它。" 'Yellow'
    $ahead = 0
}
elseif ([int]$ahead -eq 0) {
    Say '没有要推的提交，本地与远端一致。' 'Green'
    exit 0
}
else {
    Say "本地领先 $ahead 个提交。"
}

$env:GIT_SSH_COMMAND = "ssh -o ConnectTimeout=$Timeout -o StrictHostKeyChecking=accept-new"
$output = & git push $Remote $Branch 2>&1 | Out-String
$code = $LASTEXITCODE

if ($code -eq 0) {
    Say '推送完成。' 'Green'
    exit 0
}

Say $output.Trim() 'DarkGray'

if ($output -match 'Connection timed out|Connection refused|Could not resolve host|Network is unreachable') {
    Say '' 
    Say '网络到不了远端。这台机器上的具体表现：github.com 的 22 端口超时，443 也不通。' 'Red'
    Say '等网络恢复再跑一次；挂了代理的话要让 git 知道（http.proxy / GIT_SSH_COMMAND 里的 ProxyCommand）。'
    exit 3
}

if ($output -match 'Permission denied|publickey|Authentication failed|access rights') {
    Say ''
    Say '远端不认这台机器的身份。用 ssh -T git@github.com 看它认为你是谁。' 'Red'
    exit 4
}

if ($output -match 'non-fast-forward|rejected|fetch first') {
    Say ''
    Say '远端有新提交。先 git pull --rebase，再推；这里不会替你强推。' 'Red'
    exit 5
}

Say '推送失败，原因不在上面三类里，看上面的原始输出。' 'Red'
exit 1
