#requires -Version 7
<#
    冒烟模块：flowloops。由 tests\smoke.ps1 载入（pwsh tests\smoke.ps1 -Only flowloops 只跑这一个）。
    依赖 common.ps1 提供的：$Exe / $target / $targetInScript / Check。
#>

    # ------------------------------------------------------------------ loops and variables on a real window
    # A list variable drives a foreach that types every item, and a read-text captures what is on
    # screen into a variable a later step interpolates.
    Write-Host "`n-- loops and variables --"
    $loopFlow = Join-Path $env:TEMP "km-smoke-loop-$PID.json"
    $loopJson = '{"format":"keymouse-flow","version":1,' +
        '"variables":{"rows":["Alpha","Beta","Gamma"]},' +
        '"steps":[' +
        '{"type":"focus","target":{"process":"KeyMouse.SmokeTarget"}},' +
        '{"type":"key","combo":"ctrl+a","target":{"process":"KeyMouse.SmokeTarget"}},' +
        '{"type":"key","text":"delete","target":{"process":"KeyMouse.SmokeTarget"}},' +
        '{"type":"foreach","in":"rows","steps":[' +
        '{"type":"type","text":"{{item}}","target":{"process":"KeyMouse.SmokeTarget"}},' +
        '{"type":"key","text":"enter","target":{"process":"KeyMouse.SmokeTarget"}}]},' +
        '{"type":"key","combo":"ctrl+a","target":{"process":"KeyMouse.SmokeTarget"}},' +
        '{"type":"key","combo":"ctrl+c","target":{"process":"KeyMouse.SmokeTarget"}}]}'
    Set-Content -LiteralPath $loopFlow -Value $loopJson -Encoding utf8
    $loopOut = & $Exe run $loopFlow --echo 2>&1
    Check 'a foreach loop runs its body once per item' ($LASTEXITCODE -eq 0) "exit=$LASTEXITCODE :: $($loopOut -join ' / ')"
    $loopClip = Get-Clipboard -Raw
    Check '...and every item really got typed' `
        ($loopClip -match 'Alpha' -and $loopClip -match 'Beta' -and $loopClip -match 'Gamma') "clipboard=[$loopClip]"

    $captureFlow = Join-Path $env:TEMP "km-smoke-capture-$PID.json"
    $captureJson = '{"format":"keymouse-flow","version":1,"steps":[' +
        # measured: a selection inverts the colours and the read degrades (confidence 92 -> 65).
        # Click below the region first: it clears the selection and parks the caret out of the way.
        '{"type":"click","at":{"space":"client","x":100,"y":300},"target":{"process":"KeyMouse.SmokeTarget"}},' +
        '{"type":"sleep","ms":300},' +
        '{"type":"read-text","target":{"process":"KeyMouse.SmokeTarget"},' +
        '"region":{"space":"client","x":0,"y":0,"width":600,"height":40},"into":"seen"},' +
        '{"type":"key","combo":"ctrl+a","target":{"process":"KeyMouse.SmokeTarget"}},' +
        '{"type":"key","text":"delete","target":{"process":"KeyMouse.SmokeTarget"}},' +
        '{"type":"type","text":"[{{seen}}]","target":{"process":"KeyMouse.SmokeTarget"}},' +
        '{"type":"key","combo":"ctrl+a","target":{"process":"KeyMouse.SmokeTarget"}},' +
        '{"type":"key","combo":"ctrl+c","target":{"process":"KeyMouse.SmokeTarget"}}]}'
    Set-Content -LiteralPath $captureFlow -Value $captureJson -Encoding utf8
    $captureOut = & $Exe run $captureFlow --echo 2>&1
    Check 'read-text captures what it read into a variable' ($LASTEXITCODE -eq 0) "exit=$LASTEXITCODE :: $($captureOut -join ' / ')"
    $captureClip = Get-Clipboard -Raw
    Check '...and a later step interpolates it' `
        ($captureClip -match '\[' -and $captureClip -match 'Alpha') "clipboard=[$captureClip]"

    Remove-Item $loopFlow, $captureFlow -ErrorAction SilentlyContinue
