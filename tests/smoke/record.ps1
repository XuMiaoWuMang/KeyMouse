#requires -Version 7
<#
    冒烟模块：record。由 tests\smoke.ps1 载入（pwsh tests\smoke.ps1 -Only record 只跑这一个）。
    依赖 common.ps1 提供的：$Exe / $target / $targetInScript / Check。
#>

    # ------------------------------------------------------------------ record / run (JSON flow)
    # The recorder is driven with KeyMouse's own input: the hooks do not filter injected events, so a
    # synthetic session is a real session as far as the recorded JSON is concerned.
    Write-Host "`n-- record a session and replay it as a JSON flow --"
    $flowPath = Join-Path $env:TEMP "km-smoke-flow-$PID.json"
    $handFlow = Join-Path $env:TEMP "km-smoke-handflow-$PID.json"
    $badFlow = Join-Path $env:TEMP "km-smoke-badflow-$PID.json"
    Remove-Item $flowPath, $handFlow, $badFlow -ErrorAction SilentlyContinue

    $recorder = Start-Process -FilePath $Exe -ArgumentList 'record', '--out', $flowPath, '--duration', '4000', '--no-shots' `
        -NoNewWindow -PassThru
    Start-Sleep -Milliseconds 900
    $null = & $Exe mouse click left -wx 120 -wy 140 @target
    Start-Sleep -Milliseconds 250
    $null = & $Exe key type 'recorded hello' @target
    Start-Sleep -Milliseconds 300
    $null = $recorder.WaitForExit(15000)
    Check 'the recorder stops by itself with --duration' ($recorder.HasExited) 'still running'
    if (-not $recorder.HasExited) { $recorder.Kill() }

    $flow = $null
    if (Test-Path $flowPath) { try { $flow = Get-Content $flowPath -Raw | ConvertFrom-Json } catch { } }
    Check 'the recording wrote a flow document' ($null -ne $flow -and $flow.format -eq 'keymouse-flow') "format=$($flow.format)"
    $flowTypes = @($flow.steps | ForEach-Object { $_.type })
    Check 'it recorded the click' ($flowTypes -contains 'click') "types=$($flowTypes -join ',')"
    Check 'it recorded the typing' ($flowTypes -contains 'type') "types=$($flowTypes -join ',')"
    $clickStep = $flow.steps | Where-Object { $_.type -eq 'click' } | Select-Object -First 1
    Check 'the click is client-relative' ($clickStep.at.space -eq 'client') "space=$($clickStep.at.space)"
    Check 'steps name the window they happened in' `
        (@($flow.steps | Where-Object { $_.target.process -eq 'KeyMouse.SmokeTarget' }).Count -gt 0) 'no step names the target process'

    $dry = & $Exe run $flowPath --dry-run 2>&1
    Check 'replaying the recording dry-runs clean' ($LASTEXITCODE -eq 0) "exit=$LASTEXITCODE :: $($dry -join ' / ')"

    $handJson = '{"format":"keymouse-flow","version":1,"steps":[' +
        '{"type":"focus","target":{"process":"KeyMouse.SmokeTarget"}},' +
        '{"type":"click","button":"left","at":{"space":"client","x":100,"y":100},"target":{"process":"KeyMouse.SmokeTarget"}},' +
        '{"type":"type","text":"flow replay works","target":{"process":"KeyMouse.SmokeTarget"}},' +
        '{"type":"key","combo":"ctrl+a","target":{"process":"KeyMouse.SmokeTarget"}},' +
        '{"type":"key","combo":"ctrl+c","target":{"process":"KeyMouse.SmokeTarget"}}]}'
    Set-Content -LiteralPath $handFlow -Value $handJson -Encoding utf8
    $null = & $Exe run $handFlow 2>&1
    Check 'a hand-written flow replays with exit 0' ($LASTEXITCODE -eq 0) "exit=$LASTEXITCODE"
    Start-Sleep -Milliseconds 250
    Check 'the replayed flow really typed into the target' ((Get-Clipboard -Raw) -match 'flow replay works') "clipboard=[$(Get-Clipboard -Raw)]"

    Set-Content -LiteralPath $badFlow -Value '{"format":"keymouse-flow","version":1,"steps":[{"type":"teleport"}]}' -Encoding utf8
    $null = & $Exe run $badFlow 2>&1
    Check 'an unknown step type is exit 2' ($LASTEXITCODE -eq 2) "exit=$LASTEXITCODE"

    # wait-text: a condition that reads a region and waits for the text to be there. The caret is
    # parked below the region first - a blinking caret makes two reads disagree (see probe tests).
    $waitKnown = 'WaitTextCheck99'
    $null = & $Exe key combo ctrl+a @target
    $null = & $Exe key press delete @target
    $null = & $Exe key type $waitKnown @target
    $null = & $Exe key press enter -n 3 @target
    Start-Sleep -Milliseconds 400

    $waitFlow = Join-Path $env:TEMP "km-smoke-wait-$PID.json"
    function WaitFlowJson([string]$wanted, [int]$timeoutMs) {
        '{"format":"keymouse-flow","version":1,"steps":[{"type":"wait-text",' +
        '"target":{"process":"KeyMouse.SmokeTarget"},"text":"' + $wanted + '","match":"contains",' +
        '"timeoutMs":' + $timeoutMs + ',"intervalMs":200,"confirm":2,' +
        '"region":{"space":"client","x":0,"y":0,"width":600,"height":40}}]}'
    }
    Set-Content -LiteralPath $waitFlow -Value (WaitFlowJson $waitKnown 8000) -Encoding utf8
    $waitOut = & $Exe run $waitFlow --echo 2>&1
    Check 'wait-text finds text that is on screen' ($LASTEXITCODE -eq 0) "exit=$LASTEXITCODE :: $($waitOut -join ' / ')"
    Check '...and says how many reads confirmed it' (($waitOut -join ' ') -match '连续 2 次') ($waitOut -join ' / ')

    $null = & $Exe run $waitFlow --dry-run 2>&1
    Check 'a wait-text flow dry-runs without reading' ($LASTEXITCODE -eq 0) "exit=$LASTEXITCODE"

    Set-Content -LiteralPath $waitFlow -Value (WaitFlowJson 'NeverAppearsXYZ' 1200) -Encoding utf8
    $missOut = & $Exe run $waitFlow 2>&1
    Check 'wait-text exits 3 when the text never appears' ($LASTEXITCODE -eq 3) "exit=$LASTEXITCODE :: $($missOut -join ' / ')"
    Check '...and the failure says what it read instead' (($missOut -join ' ') -match '没等到') ($missOut -join ' / ')
    Remove-Item $waitFlow -ErrorAction SilentlyContinue

    Remove-Item $flowPath, $handFlow, $badFlow -ErrorAction SilentlyContinue
