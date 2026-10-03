#requires -Version 7
<#
    冒烟模块：features。由 tests\smoke.ps1 载入（pwsh tests\smoke.ps1 -Only features 只跑这一个）。
    依赖 common.ps1 提供的：$Exe / $target / $targetInScript / Check。
#>

    Write-Host "`n== window inspect =="
    $inspectOut = & $Exe window inspect @target 2>&1
    Check 'inspect finds the target and calls it usable' (($inspectOut -join "`n") -match '判定：可用') 'no usable verdict'
    Check 'inspect exits 0 when something is usable' ($LASTEXITCODE -eq 0) "exit=$LASTEXITCODE"
    $null = & $Exe window inspect --process definitely-not-running-xyz 2>&1
    Check 'inspect exits 3 when nothing matches' ($LASTEXITCODE -eq 3) "exit=$LASTEXITCODE"

    Write-Host "`n== window-relative move =="
    $moveOut = & $Exe mouse move -wx 200 -wy 150 @target 2>&1
    Check 'a client-relative move runs' ($LASTEXITCODE -eq 0) "exit=$LASTEXITCODE`n$moveOut"
    if ($moveOut -match '屏幕 (\d+),(\d+)') {
        $screenX = $Matches[1]; $screenY = $Matches[2]
        $pos = (& $Exe mouse pos).Trim()
        Check 'the reported screen point is where the cursor ended up' ($pos -eq "$screenX,$screenY") "pos=$pos expected=$screenX,$screenY"
    } else {
        Check 'the move reports the screen point it used' $false "output was [$moveOut]"
    }

    Write-Host "`n== a script focuses once and inherits the target =="
    $inheritScript = Join-Path $env:TEMP 'keymouse-smoke-inherit.txt'
    @(
        'window focus ' + $targetInScript
        'key type "inherited target works"'
        'key press enter'
        'key type "second line, same window"'
    ) | Set-Content -Path $inheritScript -Encoding utf8

    $null = & $Exe key combo ctrl+a @target
    $null = & $Exe key press delete @target
    Start-Sleep -Milliseconds 200

    $inheritOut = & $Exe run $inheritScript 2>&1
    Check 'a script runs with the selector written once' ($LASTEXITCODE -eq 0) "exit=$LASTEXITCODE`n$inheritOut"
    Check 'the log says the target was inherited' (($inheritOut -join "`n") -match '继承目标') 'no inheritance note'

    Set-Clipboard -Value '<<EMPTY>>'
    $null = & $Exe key combo ctrl+a @target
    $null = & $Exe key combo ctrl+c @target
    Start-Sleep -Milliseconds 300
    $inherited = Get-Clipboard -Raw
    Check 'inherited commands really reached the target' ($inherited -match 'inherited target works') "got [$inherited]"
    Remove-Item $inheritScript -Force -ErrorAction SilentlyContinue

