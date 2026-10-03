#requires -Version 7
<#
    冒烟模块：act。由 tests\smoke.ps1 载入（pwsh tests\smoke.ps1 -Only act 只跑这一个）。
    依赖 common.ps1 提供的：$Exe / $target / $targetInScript / Check。
#>

    # ------------------------------------------------------------------ find text and act on it
    # The step from "read the screen" to "act on what you saw": probe reports where the text is, and
    # click-text clicks the middle of that box.
    Write-Host "`n-- probe --find / click-text / when --"
    $findKnown = 'FindTarget77'
    # The serve section above ran a console process in this window, so the target has to be brought
    # back to the front before typing at it (the focus gate would otherwise refuse with exit 5).
    $null = & $Exe window focus @target
    Start-Sleep -Milliseconds 200
    $null = & $Exe key combo ctrl+a @target
    $null = & $Exe key press delete @target
    $null = & $Exe key type $findKnown @target
    $null = & $Exe key press enter -n 3 @target
    Start-Sleep -Milliseconds 400

    $findJson = (& $Exe probe @target --region 0,0,600,40 --find $findKnown --json 2>&1) -join "`n"
    $findExit = $LASTEXITCODE
    Check 'probe --find finds text that is on screen' ($findExit -eq 0) "exit=$findExit :: $($findJson -replace "`n", ' ')"
    $found = $null
    try { $found = $findJson | ConvertFrom-Json } catch { }
    Check '...and reports that it found it' ($null -ne $found -and $found.find.found -eq $true) 'find.found is not true'
    Check '...with a click point in client coordinates' `
        ($found.find.at.space -eq 'client' -and $found.find.at.x -gt 0 -and $found.find.at.y -gt 0) `
        "at=$($found.find.at.space) $($found.find.at.x),$($found.find.at.y)"
    Check '...and the box it sits in' ($found.find.rect[2] -gt 0 -and $found.find.rect[3] -gt 0) "rect=$($found.find.rect -join ',')"

    $null = & $Exe probe @target --region 0,0,600,40 --find 'NeverThereXYZ' 2>&1
    Check 'probe --find on absent text is exit 3 (no match, nothing sent)' ($LASTEXITCODE -eq 3) "exit=$LASTEXITCODE"

    $actFlow = Join-Path $env:TEMP "km-smoke-act-$PID.json"
    $actJson = '{"format":"keymouse-flow","version":1,"steps":[' +
        '{"type":"click-text","target":{"process":"KeyMouse.SmokeTarget"},"button":"left","text":"' + $findKnown + '",' +
        '"match":"contains","timeoutMs":8000,"intervalMs":200,"confirm":2,' +
        '"region":{"space":"client","x":0,"y":0,"width":600,"height":40}},' +
        '{"type":"key","combo":"ctrl+a","target":{"process":"KeyMouse.SmokeTarget"}},' +
        '{"type":"key","combo":"ctrl+c","target":{"process":"KeyMouse.SmokeTarget"}}]}'
    Set-Content -LiteralPath $actFlow -Value $actJson -Encoding utf8
    $actOut = & $Exe run $actFlow --echo 2>&1
    Check 'click-text clicks the text it saw' ($LASTEXITCODE -eq 0) "exit=$LASTEXITCODE :: $($actOut -join ' / ')"
    Check '...and the click landed on the target (its text comes back on the clipboard)' `
        ((Get-Clipboard -Raw) -match $findKnown) "clipboard=[$(Get-Clipboard -Raw)]"

    $whenFlow = Join-Path $env:TEMP "km-smoke-when-$PID.json"
    $whenJson = '{"format":"keymouse-flow","version":1,"steps":[' +
        '{"type":"sleep","ms":10,"when":{"target":{"process":"KeyMouse.SmokeTarget"},"text":"' + $findKnown + '",' +
        '"region":{"space":"client","x":0,"y":0,"width":600,"height":40},' +
        '"match":"contains","timeoutMs":4000,"intervalMs":200,"confirm":2,"else":"fail"}},' +
        '{"type":"sleep","ms":10,"when":{"target":{"process":"KeyMouse.SmokeTarget"},"text":"NeverThereXYZ",' +
        '"region":{"space":"client","x":0,"y":0,"width":600,"height":40},' +
        '"timeoutMs":800,"intervalMs":200,"confirm":1,"else":"skip"}}]}'
    Set-Content -LiteralPath $whenFlow -Value $whenJson -Encoding utf8
    $whenOut = & $Exe run $whenFlow --echo 2>&1
    Check 'a precondition that holds lets its step run' ($LASTEXITCODE -eq 0) "exit=$LASTEXITCODE :: $($whenOut -join ' / ')"
    Check '...and one that does not is skipped, not failed' (($whenOut -join ' ') -match 'skip') ($whenOut -join ' / ')

    Set-Content -LiteralPath $whenFlow -Value ($whenJson.Replace('"else":"skip"', '"else":"fail"')) -Encoding utf8
    $failOut = & $Exe run $whenFlow 2>&1
    Check 'else=fail turns an unmet precondition into exit 3' ($LASTEXITCODE -eq 3) "exit=$LASTEXITCODE :: $($failOut -join ' / ')"
    Remove-Item $actFlow, $whenFlow -ErrorAction SilentlyContinue
