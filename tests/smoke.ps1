#requires -Version 7
<#
    Desktop smoke test: drives a real Notepad window through KeyMouse and reads the result
    back through the clipboard. Needs an interactive desktop session.

        pwsh tests\smoke.ps1 [-Exe dist\KeyMouse.exe]

    It only touches a Notepad window it starts itself, and it restores your clipboard.
    Exit code 0 = all checks passed.
#>
[CmdletBinding()]
param([string]$Exe = (Join-Path $PSScriptRoot '..\dist\KeyMouse.exe'))

$ErrorActionPreference = 'Stop'
$script:passed = 0
$script:failed = 0

function Check([string]$what, [bool]$ok, [string]$detail = '') {
    if ($ok) { $script:passed++; Write-Host "  ok   $what" }
    else { $script:failed++; Write-Host "  FAIL $what  -> $detail" }
}

if (-not (Test-Path $Exe)) { Write-Error "KeyMouse.exe not found at $Exe - build it first (.\build.ps1)"; exit 1 }
$Exe = (Resolve-Path $Exe).Path
Write-Host "smoke testing $Exe`n"

$savedClipboard = Get-Clipboard -Raw -ErrorAction SilentlyContinue
$scriptFile = Join-Path $env:TEMP 'keymouse-smoke.txt'
$expected1 = 'line one: smoke test 中文也可以'
$expected2 = 'line two: after Enter'

try {
    Write-Host '== exit codes =='
    $null = & $Exe mouse click left --title 'no-such-window-xyz' 2>&1
    Check 'unknown selector exits 3' ($LASTEXITCODE -eq 3) "exit=$LASTEXITCODE"

    $null = & $Exe mouse move abc 2>&1
    Check 'bad argument exits 2' ($LASTEXITCODE -eq 2) "exit=$LASTEXITCODE"

    $null = & $Exe key press nosuchkey 2>&1
    Check 'unknown key exits 2' ($LASTEXITCODE -eq 2) "exit=$LASTEXITCODE"

    $null = & $Exe key press f24 --focus-policy none --title 'no-such-window-xyz' 2>&1
    Check 'unresolvable target exits 3' ($LASTEXITCODE -eq 3) "exit=$LASTEXITCODE"

    Write-Host "`n== scripted typing round-trip =="
    Get-Process notepad -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Milliseconds 800
    Start-Process notepad
    Start-Sleep -Seconds 3

    # Notepad restores the previous unsaved tab, so clear it first.
    $null = & $Exe key combo ctrl+a --process notepad
    $null = & $Exe key press delete --process notepad
    Start-Sleep -Milliseconds 300

    @(
        'sleep 300'
        '# client 200,200 is inside the text area in Notepad (the toolbar band is above it)'
        'mouse click left -wx 200 -wy 200 --process notepad'
        "key type `"$expected1`" --process notepad"
        'key press enter --process notepad'
        "key type `"$expected2`" --process notepad"
    ) | Set-Content -Path $scriptFile -Encoding utf8

    $out = & $Exe run $scriptFile 2>&1
    Check 'script exits 0' ($LASTEXITCODE -eq 0) "exit=$LASTEXITCODE`n$out"

    Set-Clipboard -Value '<<EMPTY>>'
    $null = & $Exe key combo ctrl+a --process notepad
    $null = & $Exe key combo ctrl+c --process notepad
    Start-Sleep -Milliseconds 300

    $got = (Get-Clipboard -Raw).Trim() -replace "`r`n", "`n"
    $want = "$expected1`n$expected2"
    Check 'typed text round-trips exactly' ($got -eq $want) "got [$got]"

    Write-Host "`n== window-relative drag =="
    $dragScript = Join-Path $env:TEMP 'keymouse-smoke-drag.txt'
    $dragLines = @('mouse click left -wx 200 -wy 200 --process notepad')
    for ($i = 1; $i -le 8; $i++) {
        $dragLines += "key type `"drag line $i`" --process notepad"
        $dragLines += 'key press enter --process notepad'
    }
    $dragLines += 'mouse drag -wx 5 -wy 130 --wx2 900 -wy2 300 --process notepad --duration 400 --steps 20'
    $dragLines | Set-Content -Path $dragScript -Encoding utf8

    $null = & $Exe key combo ctrl+a --process notepad
    $null = & $Exe key press delete --process notepad
    Start-Sleep -Milliseconds 300

    $dragOut = & $Exe run $dragScript 2>&1
    Check 'a window-relative drag runs' ($LASTEXITCODE -eq 0) "exit=$LASTEXITCODE`n$dragOut"

    Set-Clipboard -Value '<<EMPTY>>'
    $null = & $Exe key combo ctrl+c --process notepad
    Start-Sleep -Milliseconds 300
    $selection = Get-Clipboard -Raw
    Check 'the drag really selected text' (($selection -ne '<<EMPTY>>') -and ($selection -match 'drag line')) "got [$selection]"

    @('mouse drag -wx 1 -wy 1 --wx2 2') | Set-Content -Path $dragScript -Encoding utf8
    $null = & $Exe run $dragScript 2>&1
    Check 'an incomplete relative drag exits 2' ($LASTEXITCODE -eq 2) "exit=$LASTEXITCODE"
    Remove-Item $dragScript -Force -ErrorAction SilentlyContinue

    $null = & $Exe key combo ctrl+a --process notepad
    $null = & $Exe key press delete --process notepad
    Start-Sleep -Milliseconds 250

    Write-Host "`n== variables =="
    $varScript = Join-Path $env:TEMP 'keymouse-smoke-vars.txt'
    @('mouse move ${x} ${y}', 'mouse pos') | Set-Content -Path $varScript -Encoding utf8
    $before = (& $Exe mouse pos).Trim()
    $varOut = & $Exe run $varScript --set x=321 --set y=234 2>&1
    Check 'variable substitution runs' ($LASTEXITCODE -eq 0) "exit=$LASTEXITCODE`n$varOut"
    $movedTo = (& $Exe mouse pos).Trim()
    Check 'the cursor really moved to the substituted coordinates' ($movedTo -eq '321,234') "pos=$movedTo"
    Remove-Item $varScript -Force -ErrorAction SilentlyContinue

    Write-Host "`n== retry only repeats what sent nothing =="
    $retryScript = Join-Path $env:TEMP 'keymouse-smoke-retry.txt'
    @('key press f24 --process definitely-not-running-xyz') | Set-Content -Path $retryScript -Encoding utf8
    $retryReport = Join-Path $env:TEMP 'keymouse-smoke-report.json'
    Remove-Item $retryReport -Force -ErrorAction SilentlyContinue
    $retryOut = & $Exe run $retryScript --retry 2 --retry-delay 20 --report $retryReport 2>&1
    Check 'a retryable failure still fails after the retries' ($LASTEXITCODE -eq 3) "exit=$LASTEXITCODE"
    Check 'both retries are reported' ((($retryOut -join "`n") -match '第 1 次重试') -and (($retryOut -join "`n") -match '第 2 次重试')) 'retry lines missing'
    Check 'a command that kept failing is not reported as succeeded' ((($retryOut -join "`n") -notmatch '后成功')) 'the note claims success for a failed command'
    Check 'report file was written' (Test-Path $retryReport) "missing $retryReport"
    if (Test-Path $retryReport) {
        $json = Get-Content $retryReport -Raw | ConvertFrom-Json
        Check 'report counts the attempts' ($json.commands[0].attempts -eq 3) "attempts=$($json.commands[0].attempts)"
        Check 'report proves nothing was injected' ($json.commands[0].injectedEvents -eq 0) "injected=$($json.commands[0].injectedEvents)"
        Check 'report records the exit code' ($json.exitCode -eq 3) "exitCode=$($json.exitCode)"
    }
    Remove-Item $retryScript, $retryReport -Force -ErrorAction SilentlyContinue
    & $Exe mouse move ($before -split ',')[0] ($before -split ',')[1] | Out-Null

    Write-Host "`n== waits =="
    $waitScript = Join-Path $env:TEMP 'keymouse-smoke-wait.txt'
    @('waitfor --process notepad --timeout 3000', 'waitgone --process definitely-not-running-xyz --timeout 500') |
        Set-Content -Path $waitScript -Encoding utf8
    $waitOut = & $Exe run $waitScript 2>&1
    Check 'waitfor finds the running Notepad and waitgone agrees' ($LASTEXITCODE -eq 0) "exit=$LASTEXITCODE`n$waitOut"
    Check 'the wait reports how long it took' (($waitOut -join "`n") -match '在 \d+ms 后满足') 'no timing line'

    @('waitfor --process definitely-not-running-xyz --timeout 400 --interval 100') |
        Set-Content -Path $waitScript -Encoding utf8
    $null = & $Exe run $waitScript 2>&1
    Check 'a waitfor timeout exits 3' ($LASTEXITCODE -eq 3) "exit=$LASTEXITCODE"

    @('key press f24 --process notepad', 'waitfor --timeout 100') | Set-Content -Path $waitScript -Encoding utf8
    $null = & $Exe run $waitScript 2>&1
    Check 'a bad wait line aborts before anything runs' ($LASTEXITCODE -eq 2) "exit=$LASTEXITCODE"
    Remove-Item $waitScript -Force -ErrorAction SilentlyContinue

    Write-Host "`n== a disabled window is refused =="
    Add-Type -Namespace KeyMouseSmoke -Name Win -MemberDefinition '[DllImport("user32.dll")] public static extern bool EnableWindow(IntPtr h, bool e);'
    $note = Get-Process notepad | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
    [void][KeyMouseSmoke.Win]::EnableWindow($note.MainWindowHandle, $false)
    Start-Sleep -Milliseconds 300
    $null = & $Exe key press f24 --process notepad 2>&1
    Check 'a disabled window exits 4' ($LASTEXITCODE -eq 4) "exit=$LASTEXITCODE"
    Check 'the listing flags it as disabled' (((& $Exe window list --process notepad) -join "`n") -match '已禁用') 'no disabled flag'
    [void][KeyMouseSmoke.Win]::EnableWindow($note.MainWindowHandle, $true)
    Start-Sleep -Milliseconds 300
    $null = & $Exe window focus --process notepad 2>&1
    Check 're-enabling makes it usable again' ($LASTEXITCODE -eq 0) "exit=$LASTEXITCODE"

    Write-Host "`n== dry run sends nothing =="
    $null = & $Exe key combo ctrl+a --process notepad
    $null = & $Exe key press delete --process notepad
    Start-Sleep -Milliseconds 250
    $dry = & $Exe run $scriptFile --dry-run 2>&1
    Check 'dry run exits 0' ($LASTEXITCODE -eq 0) "exit=$LASTEXITCODE"
    # -match on an array returns the matching elements, so collapse it to one string first.
    Check 'dry run reports suppressed input' (($dry -join "`n") -match '共抑制 \d+ 个输入事件') 'no suppression line'

    Set-Clipboard -Value '<<EMPTY>>'
    $null = & $Exe key combo ctrl+a --process notepad
    $null = & $Exe key combo ctrl+c --process notepad
    Start-Sleep -Milliseconds 300
    # An empty document copies nothing, so the sentinel surviving is the expected result too.
    $after = Get-Clipboard -Raw
    Check 'dry run left the document empty' (($after -eq '<<EMPTY>>') -or ($after.Trim() -eq '')) "got [$after]"
}
finally {
    Get-Process notepad -ErrorAction SilentlyContinue | Stop-Process -Force
    Remove-Item $scriptFile -Force -ErrorAction SilentlyContinue
    if ($null -ne $savedClipboard) { Set-Clipboard -Value $savedClipboard }
}

Write-Host "`n$script:passed passed, $script:failed failed"
exit ([int]($script:failed -gt 0))
