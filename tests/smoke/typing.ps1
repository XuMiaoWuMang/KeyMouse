#requires -Version 7
<#
    冒烟模块：typing。由 tests\smoke.ps1 载入（pwsh tests\smoke.ps1 -Only typing 只跑这一个）。
    依赖 common.ps1 提供的：$Exe / $target / $targetInScript / Check。
#>

    Write-Host "`n== scripted typing round-trip =="
    $expected1 = 'line one: smoke test 中文也可以'
    $expected2 = 'line two: after Enter'
    $scriptFile = Join-Path $env:TEMP 'keymouse-smoke.txt'

    $null = & $Exe key combo ctrl+a @target
    $null = & $Exe key press delete @target
    Start-Sleep -Milliseconds 200

    @(
        'sleep 300'
        'mouse click left -wx 100 -wy 100 ' + $targetInScript
        "key type `"$expected1`" " + $targetInScript
        'key press enter ' + $targetInScript
        "key type `"$expected2`" " + $targetInScript
    ) | Set-Content -Path $scriptFile -Encoding utf8

    $out = & $Exe run $scriptFile 2>&1
    Check 'script exits 0' ($LASTEXITCODE -eq 0) "exit=$LASTEXITCODE`n$out"

    Set-Clipboard -Value '<<EMPTY>>'
    $null = & $Exe key combo ctrl+a @target
    $null = & $Exe key combo ctrl+c @target
    Start-Sleep -Milliseconds 300

    $got = (Get-Clipboard -Raw).Trim() -replace "`r`n", "`n"
    $want = "$expected1`n$expected2"
    Check 'typed text round-trips exactly' ($got -eq $want) "got [$got]"

    Write-Host "`n== window-relative drag =="
    $dragScript = Join-Path $env:TEMP 'keymouse-smoke-drag.txt'
    $dragLines = @('mouse click left -wx 100 -wy 100 ' + $targetInScript)
    for ($i = 1; $i -le 10; $i++) {
        $dragLines += "key type `"drag line $i`" " + $targetInScript
        $dragLines += 'key press enter ' + $targetInScript
    }
    $dragLines += 'mouse drag -wx 5 -wy 20 --wx2 500 -wy2 160 ' + $targetInScript + ' --duration 400 --steps 20'
    $dragLines | Set-Content -Path $dragScript -Encoding utf8

    $null = & $Exe key combo ctrl+a @target
    $null = & $Exe key press delete @target
    Start-Sleep -Milliseconds 200

    $dragOut = & $Exe run $dragScript 2>&1
    Check 'a window-relative drag runs' ($LASTEXITCODE -eq 0) "exit=$LASTEXITCODE`n$dragOut"

    Set-Clipboard -Value '<<EMPTY>>'
    $null = & $Exe key combo ctrl+c @target
    Start-Sleep -Milliseconds 300
    $selection = Get-Clipboard -Raw
    Check 'the drag really selected text' (($selection -ne '<<EMPTY>>') -and ($selection -match 'drag line')) "got [$selection]"

    @('mouse drag -wx 1 -wy 1 --wx2 2') | Set-Content -Path $dragScript -Encoding utf8
    $null = & $Exe run $dragScript 2>&1
    Check 'an incomplete relative drag exits 2' ($LASTEXITCODE -eq 2) "exit=$LASTEXITCODE"
    Remove-Item $dragScript, $scriptFile -Force -ErrorAction SilentlyContinue

    $null = & $Exe key combo ctrl+a @target
    $null = & $Exe key press delete @target
    Start-Sleep -Milliseconds 200

    Write-Host "`n== variables =="
    $varScript = Join-Path $env:TEMP 'keymouse-smoke-vars.txt'
    @('mouse move ${x} ${y}', 'mouse pos') | Set-Content -Path $varScript -Encoding utf8
    $before = (& $Exe mouse pos).Trim()
    $varOut = & $Exe run $varScript --set x=321 --set y=234 2>&1
    Check 'variable substitution runs' ($LASTEXITCODE -eq 0) "exit=$LASTEXITCODE`n$varOut"
    $movedTo = (& $Exe mouse pos).Trim()
    Check 'the cursor really moved to the substituted coordinates' ($movedTo -eq '321,234') "pos=$movedTo"
    Remove-Item $varScript -Force -ErrorAction SilentlyContinue

