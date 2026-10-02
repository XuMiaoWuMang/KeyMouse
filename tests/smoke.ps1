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

    Write-Host "`n== dry run sends nothing =="
    $null = & $Exe key combo ctrl+a --process notepad
    $null = & $Exe key press delete --process notepad
    Start-Sleep -Milliseconds 250
    $dry = & $Exe run $scriptFile --dry-run 2>&1
    Check 'dry run exits 0' ($LASTEXITCODE -eq 0) "exit=$LASTEXITCODE"
    # -match on an array returns the matching elements, so collapse it to one string first.
    Check 'dry run reports suppressed input' (($dry -join "`n") -match 'input event\(s\) suppressed') 'no suppression line'

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
