#requires -Version 7
<#
    冒烟模块：calls（子流程）。由 tests\smoke.ps1 载入（pwsh tests\smoke.ps1 -Only calls 只跑这一个）。
    依赖 common.ps1 提供的：$Exe / $target / $targetInScript / Check。
#>

    # 数据用 ASCII：靶子的小字号下复杂汉字读不准（实测 「子流程:甲」 读成 FRE: F，置信度 44.6，
    # 把横带从 24px 加到 64px 都没用），那是读屏能力的事，不该混进子流程的用例。    Write-Host "`n-- 子流程（call）--"
    $callDir = Join-Path $env:TEMP "km-smoke-calls-$PID"
    New-Item -ItemType Directory -Force -Path $callDir | Out-Null
    # 子流程：自己的默认值 + 由调用者传进来的 word；打字后把光标挪走（实测：光标落在区域里会把整行读崩），
    # 再把第一行读进 seen 交给调用者。
    $subJson = '{"format":"keymouse-flow","version":1,"variables":{"prefix":"sub"},"steps":[' +
        '{"type":"type","text":"{{prefix}}:{{word}}","target":{"process":"KeyMouse.SmokeTarget"}},' +
        '{"type":"key","text":"enter","target":{"process":"KeyMouse.SmokeTarget"}},' +
        '{"type":"key","text":"enter","target":{"process":"KeyMouse.SmokeTarget"}},' +
        '{"type":"key","text":"enter","target":{"process":"KeyMouse.SmokeTarget"}},' +
        '{"type":"read-text","target":{"process":"KeyMouse.SmokeTarget"},' +
        '"region":{"space":"client","x":0,"y":0,"width":600,"height":40},"into":"seen"}]}'
    Set-Content -LiteralPath (Join-Path $callDir 'sub.json') -Value $subJson -Encoding utf8

    $mainJson = '{"format":"keymouse-flow","version":1,"variables":{"words":["A","B"]},"steps":[' +
        '{"type":"focus","target":{"process":"KeyMouse.SmokeTarget"}},' +
        '{"type":"key","combo":"ctrl+a","target":{"process":"KeyMouse.SmokeTarget"}},' +
        '{"type":"key","text":"delete","target":{"process":"KeyMouse.SmokeTarget"}},' +
        '{"type":"foreach","in":"words","steps":[' +
        '{"type":"call","flow":"sub.json","vars":{"word":"{{item}}"},"export":["seen"]},' +
        '{"type":"type","text":"导出={{seen}}","target":{"process":"KeyMouse.SmokeTarget"}},' +
        '{"type":"key","text":"enter","target":{"process":"KeyMouse.SmokeTarget"}},' +
        '{"type":"key","text":"enter","target":{"process":"KeyMouse.SmokeTarget"}},' +
        '{"type":"key","text":"enter","target":{"process":"KeyMouse.SmokeTarget"}}]},' +
        '{"type":"key","combo":"ctrl+a","target":{"process":"KeyMouse.SmokeTarget"}},' +
        '{"type":"key","combo":"ctrl+c","target":{"process":"KeyMouse.SmokeTarget"}}]}'
    Set-Content -LiteralPath (Join-Path $callDir 'main.json') -Value $mainJson -Encoding utf8

    $callOut = & $Exe run (Join-Path $callDir 'main.json') --echo 2>&1
    Check 'a subflow runs inline, once per loop round' ($LASTEXITCODE -eq 0) "exit=$LASTEXITCODE :: $($callOut -join ' / ')"
    $callClip = Get-Clipboard -Raw
    Check "the call's vars reach inside the subflow" `
        ($callClip -match 'sub:A' -and $callClip -match 'sub:B') "clipboard=[$callClip]"
    Check 'the subflow variable the caller did not pass stays the subflow default' `
        ($callClip -notmatch ':\{\{prefix\}\}') "clipboard=[$callClip]"
    Check '...and its export comes back to the caller' ($callClip -match '导出=sub:A') "clipboard=[$callClip]"

    # 循环里不该漏出来：同一份主流程去掉 export，调用者的 seen 就不会被赋值（会以未定义变量报错）
    $noExport = Join-Path $callDir 'no-export.json'
    Set-Content -LiteralPath $noExport -Encoding utf8 -Value ($mainJson.Replace('"export":["seen"]', '""').Replace('"export":["seen"],', ''))
    $null = & $Exe run $noExport 2>&1
    Check 'a capture inside a subflow is refused outside it when it was not exported' `
        ($LASTEXITCODE -eq 2) "exit=$LASTEXITCODE"

    Remove-Item $callDir -Recurse -Force -ErrorAction SilentlyContinue
