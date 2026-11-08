#requires -Version 7
<#
    冒烟模块：controlflow。由 tests\smoke\run.ps1 载入（-Only controlflow 只跑这一个）。
    依赖 common.ps1 提供的：$Exe / $RunDir / Check。

    这一篇验的是"条件真的读到了屏幕、分支真的改变路线、循环真的跑了"：
    text() 走真实 OCR，while/for 走真实执行器，报告里的 origin 落回文档行。
#>

Write-Host "`n-- control flow on a real window --"
$flow = Join-Path $RunDir "km-smoke-controlflow-$PID.json"
$report = Join-Path $RunDir "km-smoke-controlflow-$PID.report.json"
$probe = '"probe":{"target":{"process":"KeyMouse.SmokeTarget"},' +
    '"region":{"space":"client","x":0,"y":0,"width":600,"height":40},"match":"contains"}'

$json = '{"format":"keymouse-flow","version":1,' +
    '"variables":{"n":"0","sum":"0","seen":""},' +
    '"steps":[' +
    '{"type":"focus","target":{"process":"KeyMouse.SmokeTarget"}},' +
    '{"type":"key","combo":"ctrl+a","target":{"process":"KeyMouse.SmokeTarget"}},' +
    '{"type":"key","text":"delete","target":{"process":"KeyMouse.SmokeTarget"}},' +
    '{"type":"type","text":"FLOW-42","target":{"process":"KeyMouse.SmokeTarget"}},' +
    '{"type":"sleep","ms":600},' +
    '{"type":"if","cond":"text(\"FLOW-42\")",' + $probe + ',' +
        '"steps":[{"type":"set","name":"seen","value":"\"yes\""}],' +
        '"else":[{"type":"set","name":"seen","value":"\"no\""}]},' +
    '{"type":"while","cond":"n < 2","steps":[{"type":"set","name":"n","value":"n + 1"}]},' +
    '{"type":"for","init":{"i":"0"},"cond":"i < 2","step":{"i":"i + 1"},' +
        '"steps":[{"type":"set","name":"sum","value":"sum + i"}]},' +
    '{"type":"assert","cond":"seen == \"yes\"","message":"条件没有读到屏幕上的 FLOW-42"},' +
    '{"type":"assert","cond":"n == 2","message":"while 没有跑两轮"},' +
    '{"type":"assert","cond":"sum == 1","message":"for 的计数不对"},' +
    '{"type":"key","combo":"ctrl+a","target":{"process":"KeyMouse.SmokeTarget"}},' +
    '{"type":"key","text":"delete","target":{"process":"KeyMouse.SmokeTarget"}}]}'
Set-Content -LiteralPath $flow -Value $json -Encoding utf8

$out = & $Exe run $flow --echo --report $report 2>&1
Check 'if/while/for run against a real window' ($LASTEXITCODE -eq 0) "exit=$LASTEXITCODE :: $($out -join ' / ')"

if (Test-Path $report) {
    $r = Get-Content -LiteralPath $report -Raw -Encoding utf8 | ConvertFrom-Json
    Check '...the then-branch was the one that ran' `
        (@($r.commands | Where-Object { $_.origin -eq '6.1' }).Count -eq 1) `
        "origins=$((@($r.commands) | ForEach-Object { $_.origin }) -join ',')"
    Check '...while ran its body twice' `
        (@($r.commands | Where-Object { $_.origin -eq '7.1' }).Count -eq 2) `
        "origins=$((@($r.commands) | ForEach-Object { $_.origin }) -join ',')"
    Check '...and the report keeps the dynamic path honest' `
        ($r.succeeded -ge 8 -and $r.exitCode -eq 0) "succeeded=$($r.succeeded) failed=$($r.failed)"
    Check '...dynamic flows print no denominator instead of lying' `
        (($out -join "`n") -match '\[\s+1\]' -and ($out -join "`n") -notmatch '\[\s+1/') `
        ($out -join ' / ')
}
else {
    Check '...the report file was written' $false "missing $report"
}

Remove-Item $flow, $report -ErrorAction SilentlyContinue
