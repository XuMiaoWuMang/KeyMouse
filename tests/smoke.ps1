#requires -Version 7
<#
    Desktop smoke test: drives a real window through KeyMouse and reads the result back
    through the clipboard. Needs an interactive desktop session.

        dotnet build KeyMouse.sln -c Release
        pwsh tests\smoke.ps1                                  # uses dist\KeyMouse.exe

    It starts KeyMouse.SmokeTarget, drives that window, and stops that process again.
    It never touches the applications you have open, and it restores your clipboard.
    Exit code 0 = all checks passed.
#>
[CmdletBinding()]
param(
    [string]$Exe = (Join-Path $PSScriptRoot '..\dist\KeyMouse.exe'),
    [string]$TargetExe = (Join-Path $PSScriptRoot 'KeyMouse.SmokeTarget\bin\Release\net10.0-windows\KeyMouse.SmokeTarget.exe')
)

$ErrorActionPreference = 'Stop'
$script:passed = 0
$script:failed = 0

function Check([string]$what, [bool]$ok, [string]$detail = '') {
    if ($ok) { $script:passed++; Write-Host "  ok   $what" }
    else { $script:failed++; Write-Host "  FAIL $what  -> $detail" }
}

if (-not (Test-Path $Exe)) { Write-Error "KeyMouse.exe not found at $Exe - build it first (.\build.ps1)"; exit 1 }
if (-not (Test-Path $TargetExe)) { Write-Error "smoke target not found at $TargetExe - run: dotnet build KeyMouse.sln -c Release"; exit 1 }
$Exe = (Resolve-Path $Exe).Path
$TargetExe = (Resolve-Path $TargetExe).Path
Write-Host "smoke testing $Exe`n"

# The exe answers in the console's code page, and PowerShell decodes native output with
# [Console]::OutputEncoding - the two agree by default, so nothing is forced here. A host that
# has pinned them apart would otherwise fail every Chinese assertion for a reason that has
# nothing to do with the program, so check the round-trip once and say so plainly.
$probe = (& $Exe mouse move 1 1 2>&1) -join ''
if ($probe -notmatch '已移动到') {
    Write-Host "  cannot decode the program's Chinese output."
    Write-Host "  expected [已移动到 1,1] but got [$probe]"
    Write-Host "  console code page $(try { (chcp) } catch { '?' })" +
                " vs [Console]::OutputEncoding $([Console]::OutputEncoding.WebName)"
    exit 1
}
Check 'Chinese output decodes correctly in this host' $true

$savedClipboard = Get-Clipboard -Raw -ErrorAction SilentlyContinue
$targetTitle = 'KeyMouse 冒烟靶子'
$target = @('--title', $targetTitle)                 # when calling the tool directly
$targetInScript = '--title "' + $targetTitle + '"'   # inside a script line the space needs quotes

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

    Write-Host "`n== the smoke target =="
    Get-Process KeyMouse.SmokeTarget -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Process -FilePath $TargetExe
    $deadline = (Get-Date).AddSeconds(20)
    do {
        Start-Sleep -Milliseconds 300
        $null = & $Exe window focus @target 2>&1
    } while ($LASTEXITCODE -ne 0 -and (Get-Date) -lt $deadline)
    Check 'the target window is up and can be focused' ($LASTEXITCODE -eq 0) "exit=$LASTEXITCODE"

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
    @("waitfor --title `"$targetTitle`" --timeout 3000", 'waitgone --process definitely-not-running-xyz --timeout 500') |
        Set-Content -Path $waitScript -Encoding utf8
    $waitOut = & $Exe run $waitScript 2>&1
    Check 'waitfor finds the target and waitgone agrees' ($LASTEXITCODE -eq 0) "exit=$LASTEXITCODE`n$waitOut"
    Check 'the wait reports how long it took' (($waitOut -join "`n") -match '在 \d+ms 后满足') 'no timing line'

    @('waitfor --process definitely-not-running-xyz --timeout 400 --interval 100') |
        Set-Content -Path $waitScript -Encoding utf8
    $null = & $Exe run $waitScript 2>&1
    Check 'a waitfor timeout exits 3' ($LASTEXITCODE -eq 3) "exit=$LASTEXITCODE"

    @('key press f24 ' + $targetInScript, 'waitfor --timeout 100') | Set-Content -Path $waitScript -Encoding utf8
    $null = & $Exe run $waitScript 2>&1
    Check 'a bad wait line aborts before anything runs' ($LASTEXITCODE -eq 2) "exit=$LASTEXITCODE"
    Remove-Item $waitScript -Force -ErrorAction SilentlyContinue

    Write-Host "`n== a disabled window is refused =="
    Add-Type -Namespace KeyMouseSmoke -Name Win -MemberDefinition '[DllImport("user32.dll")] public static extern bool EnableWindow(IntPtr h, bool e);'
    $note = Get-Process KeyMouse.SmokeTarget | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
    [void][KeyMouseSmoke.Win]::EnableWindow($note.MainWindowHandle, $false)
    Start-Sleep -Milliseconds 300
    $null = & $Exe key press f24 @target 2>&1
    Check 'a disabled window exits 4' ($LASTEXITCODE -eq 4) "exit=$LASTEXITCODE"
    Check 'the listing flags it as disabled' (((& $Exe window list --process KeyMouse.SmokeTarget) -join "`n") -match '已禁用') 'no disabled flag'
    [void][KeyMouseSmoke.Win]::EnableWindow($note.MainWindowHandle, $true)
    Start-Sleep -Milliseconds 300
    $null = & $Exe window focus @target 2>&1
    Check 're-enabling makes it usable again' ($LASTEXITCODE -eq 0) "exit=$LASTEXITCODE"

    Write-Host "`n== dry run sends nothing =="
    $dryScript = Join-Path $env:TEMP 'keymouse-smoke-dry.txt'
    @('key type "SHOULD NOT APPEAR" ' + $targetInScript) | Set-Content -Path $dryScript -Encoding utf8
    $null = & $Exe key combo ctrl+a @target
    $null = & $Exe key press delete @target
    Start-Sleep -Milliseconds 200
    $dry = & $Exe run $dryScript --dry-run 2>&1
    Check 'dry run exits 0' ($LASTEXITCODE -eq 0) "exit=$LASTEXITCODE"
    Check 'dry run reports suppressed input' (($dry -join "`n") -match '共抑制 \d+ 个输入事件') 'no suppression line'

    Set-Clipboard -Value '<<EMPTY>>'
    $null = & $Exe key combo ctrl+a @target
    $null = & $Exe key combo ctrl+c @target
    Start-Sleep -Milliseconds 300
    $after = Get-Clipboard -Raw
    Check 'dry run left the document empty' (($after -eq '<<EMPTY>>') -or ($after.Trim() -eq '')) "got [$after]"
    Remove-Item $dryScript -Force -ErrorAction SilentlyContinue

    Write-Host "`n== vk: escape hatch =="
    $vkOut = & $Exe key press vk:0x87 @target 2>&1
    Check 'a raw virtual key is accepted' ($LASTEXITCODE -eq 0) "exit=$LASTEXITCODE`n$vkOut"
    $badVk = & $Exe key press vk:zz @target 2>&1
    Check 'a malformed raw virtual key is refused' ($LASTEXITCODE -eq 2) "exit=$LASTEXITCODE"

    Write-Host "`n== stdin =="
    $stdinOut = @('sleep 50', 'mouse pos') | & $Exe run - --echo 2>&1
    Check 'a script can be piped in' ($LASTEXITCODE -eq 0) "exit=$LASTEXITCODE`n$stdinOut"
    Check 'piped commands actually ran' (($stdinOut -join "`n") -match '\d+,\d+') 'no position in the output'

    Write-Host "`n== --keep-going and the report =="
    $keepScript = Join-Path $env:TEMP 'keymouse-smoke-keep.txt'
    $keepReport = Join-Path $env:TEMP 'keymouse-smoke-keep.json'
    Remove-Item $keepReport -Force -ErrorAction SilentlyContinue
    @('mouse pos', 'key press no-such-key', 'mouse pos') | Set-Content -Path $keepScript -Encoding utf8

    $null = & $Exe run $keepScript 2>&1
    Check 'without --keep-going the run stops at the bad line' ($LASTEXITCODE -eq 2) "exit=$LASTEXITCODE"

    $null = & $Exe run $keepScript --keep-going --report $keepReport 2>&1
    Check '--keep-going still returns the first failure code' ($LASTEXITCODE -eq 2) "exit=$LASTEXITCODE"
    if (Test-Path $keepReport) {
        $report = Get-Content $keepReport -Raw | ConvertFrom-Json
        Check 'every command is in the report' ($report.commands.Count -eq 3) "count=$($report.commands.Count)"
        Check 'the command after the failure still ran' ($report.commands[2].exitCode -eq 0) "exit=$($report.commands[2].exitCode)"
        Check 'the report marks the failing line' ($report.commands[1].exitCode -eq 2) "exit=$($report.commands[1].exitCode)"
        Check 'nothing was injected by the failing command' ($report.commands[1].injectedEvents -eq 0) 'injected something'
    }
    Remove-Item $keepScript, $keepReport -Force -ErrorAction SilentlyContinue

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

    Write-Host "`n== loops, and one process from start to finish =="
    $loopScript = Join-Path $env:TEMP 'keymouse-smoke-loop.txt'
    @(
        'sleep 300'
        'window focus ' + $targetInScript
        'key combo ctrl+a'
        'key press delete'
        'repeat 4 as row'
        'key type "loop line ${row}"'
        'key press enter'
        'end'
    ) | Set-Content -Path $loopScript -Encoding utf8

    # The runner dispatches every command in its own process. If it ever spawned one per
    # line, this sampling would see more than one KeyMouse process.
    $proc = Start-Process -FilePath $Exe -ArgumentList 'run', "`"$loopScript`"" -PassThru
    $counts = @()
    while (-not $proc.HasExited) {
        $counts += @(Get-Process KeyMouse -ErrorAction SilentlyContinue | ForEach-Object {
            try { if ($_.Path -eq $Exe) { $_.Id } } catch { }
        }).Count
        Start-Sleep -Milliseconds 50
    }
    Check 'the run was long enough to sample' ($counts.Count -gt 0) 'no samples taken'
    Check 'a script is exactly one process from first command to last' `
        (($counts | Measure-Object -Maximum).Maximum -le 1) "saw $(($counts | Sort-Object -Unique) -join ',')"
    Check 'the loop exits 0' ($proc.ExitCode -eq 0) "exit=$($proc.ExitCode)"

    Set-Clipboard -Value '<<EMPTY>>'
    $null = & $Exe key combo ctrl+a @target
    $null = & $Exe key combo ctrl+c @target
    Start-Sleep -Milliseconds 300
    $looped = Get-Clipboard -Raw
    Check 'the loop wrote its first iteration' ($looped -match 'loop line 0') "got [$looped]"
    Check 'the loop wrote its last iteration' ($looped -match 'loop line 3') "got [$looped]"
    Check 'the loop stopped at the requested count' ($looped -notmatch 'loop line 4') "got [$looped]"

    # Structure is checked in full before the first command, so a broken loop sends nothing.
    @('repeat 3', 'key press f24') | Set-Content -Path $loopScript -Encoding utf8
    $null = & $Exe run $loopScript 2>&1
    Check 'a repeat without end is refused before anything runs' ($LASTEXITCODE -eq 2) "exit=$LASTEXITCODE"

    @('key press f24', 'end') | Set-Content -Path $loopScript -Encoding utf8
    $null = & $Exe run $loopScript 2>&1
    Check 'a stray end is refused before anything runs' ($LASTEXITCODE -eq 2) "exit=$LASTEXITCODE"
    Remove-Item $loopScript -Force -ErrorAction SilentlyContinue

    # ------------------------------------------------------- probe (perception)
    # Reading is exercised end to end: type a known string, read the region back, and make the
    # typed text the assertion. The target's whole client area is one text box whose text starts
    # at the top-left, so 0,0,600,40 addresses the first line without guessing at the desktop.
    Write-Host "`n-- probe (perception) --"
    $engine = @(
        (Get-Command tesseract -ErrorAction SilentlyContinue).Source
        (Join-Path $env:ProgramFiles 'Tesseract-OCR\tesseract.exe')
        (Join-Path $env:LOCALAPPDATA 'Programs\Tesseract-OCR\tesseract.exe')
    ) | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1

    if (-not $engine) {
        Write-Host '  skip: no OCR engine installed; only the failure path is checked'
        $null = & $Exe probe @target --region 0,0,10,10 --engine 'definitely-not-an-engine' 2>&1
        Check 'a missing OCR engine is exit 6, not a crash' ($LASTEXITCODE -eq 6) "exit=$LASTEXITCODE"
    }
    else {
        $known = 'ProbeCheck12345'
        $null = & $Exe key combo ctrl+a @target
        $null = & $Exe key press delete @target
        $null = & $Exe key type $known @target
        Start-Sleep -Milliseconds 400

        $read = & $Exe probe @target --region 0,0,600,40 --keep-image "$env:TEMP\km-smoke-probe.bmp" 2>&1
        $readExit = $LASTEXITCODE
        $text = ($read -join ' ') -replace '\s', ''
        Check 'probe reads the text that was typed into the target' ($readExit -eq 0) "exit=$readExit :: $($read -join ' / ')"
        Check 'the read matches what was typed' ($text -match $known) "got [$text]"
        if ($text -notmatch $known) {
            Write-Host "  [diag] what was typed : [$known]"
            Write-Host "  [diag] textbox now   : [$(($(& $Exe key combo ctrl+a @target; & $Exe key combo ctrl+c @target; Start-Sleep -Milliseconds 200; Get-Clipboard -Raw) -join '') -replace '\s', '')]"
            Write-Host "  [diag] captured image: $env:TEMP\km-smoke-probe.bmp"
        }

        $json = (& $Exe probe @target --region 0,0,600,40 --json 2>&1) -join "`n"
        $jsonExit = $LASTEXITCODE
        Check 'json mode exits 0' ($jsonExit -eq 0) "exit=$jsonExit"
        Check 'json carries engine identity' `
            ($json -match '"version"' -and $json -match '"models"' -and $json -match '"tessdata"') 'engine identity missing'
        Check 'json carries the window and the region that was read' `
            ($json -match '"handle"' -and $json -match '"space"' -and $json -match '"rect"') 'window or region missing'
        Check 'json reports how many reads agreed' ($json -match '"reads"' -and $json -match '"consistent"') 'consensus fields missing'

        $null = & $Exe key combo ctrl+a @target
        $null = & $Exe key press delete @target
        Start-Sleep -Milliseconds 300
        # A region that is empty by construction: the text box's content is one line at the top, so
        # the area well below it is blank whatever the focus did.
        $empty = & $Exe probe @target --region 0,300,400,40 2>&1
        $emptyExit = $LASTEXITCODE
        Check 'an empty region exits 0' ($emptyExit -eq 0) "exit=$emptyExit :: $($empty -join ' / ')"
        Check 'an empty read is reported as a result, not as a failure' `
            (($empty -join ' ') -match '读到空') ($empty -join ' / ')

        $null = & $Exe probe --region 0,0,10,10 2>&1
        Check 'probe without a selector is exit 2' ($LASTEXITCODE -eq 2) "exit=$LASTEXITCODE"
        $null = & $Exe probe @target --region '1,2,3' 2>&1
        Check 'a malformed region is exit 2' ($LASTEXITCODE -eq 2) "exit=$LASTEXITCODE"
        $null = & $Exe probe @target --region '1,2,99999,10' 2>&1
        Check 'a region outside the client area is exit 2' ($LASTEXITCODE -eq 2) "exit=$LASTEXITCODE"
        $null = & $Exe probe --title 'nosuchwindow-xyz' --region 0,0,10,10 2>&1
        Check 'an unmatched selector is exit 3' ($LASTEXITCODE -eq 3) "exit=$LASTEXITCODE"
        $null = & $Exe probe @target --region 0,0,600,40 --engine 'definitely-not-an-engine' 2>&1
        Check 'a missing OCR engine is exit 6' ($LASTEXITCODE -eq 6) "exit=$LASTEXITCODE"
        $null = & $Exe probe @target --region 0,0,600,40 --resample 'spline' 2>&1
        Check 'an unknown resampler is exit 2' ($LASTEXITCODE -eq 2) "exit=$LASTEXITCODE"
    }

    # Region selection: --rect drives the same conversion the overlay drives, the interactive paths
    # are driven with KeyMouse's own mouse and keyboard, and the --space window origin is pinned by
    # comparing the pixels reached from both coordinate spaces.
    Write-Host "`n-- region pick (choose the region instead of guessing it) --"

    Add-Type -Namespace KeyMouseRegion -Name Win -MemberDefinition @'
[DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
[DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr h, out RECT r);
[DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr h, ref POINT p);
[StructLayout(LayoutKind.Sequential)] public struct RECT { public int L; public int T; public int R; public int B; }
[StructLayout(LayoutKind.Sequential)] public struct POINT { public int X; public int Y; }
'@

    $pickedProcess = Get-Process KeyMouse.SmokeTarget | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
    $pickedHandle = $pickedProcess.MainWindowHandle
    $windowRect = New-Object KeyMouseRegion.Win+RECT
    $null = [KeyMouseRegion.Win]::GetWindowRect($pickedHandle, [ref]$windowRect)
    $clientRect = New-Object KeyMouseRegion.Win+RECT
    $null = [KeyMouseRegion.Win]::GetClientRect($pickedHandle, [ref]$clientRect)
    $clientOrigin = New-Object KeyMouseRegion.Win+POINT
    $clientOrigin.X = 0; $clientOrigin.Y = 0
    $null = [KeyMouseRegion.Win]::ClientToScreen($pickedHandle, [ref]$clientOrigin)
    $clientWidth = $clientRect.R - $clientRect.L
    $clientHeight = $clientRect.B - $clientRect.T
    $handleText = '0x{0:X}' -f $pickedHandle.ToInt64()

    $pickJson = (& $Exe region pick --rect "$($clientOrigin.X + 60),$($clientOrigin.Y + 60),200,30" --json 2>&1) -join "`n"
    $pickExit = $LASTEXITCODE
    $pick = $null
    try { $pick = $pickJson | ConvertFrom-Json } catch { }
    Check 'region pick --rect exits 0' ($pickExit -eq 0) "exit=$pickExit :: $pickJson"
    Check 'it reports the window that owns the selection' `
        ($null -ne $pick -and $pick.window.handle -eq $handleText) "handle=$($pick.window.handle) expected=$handleText"
    Check 'it reports the region in client space' `
        ($null -ne $pick -and $pick.space -eq 'client' -and ($pick.region -join ',') -eq '60,60,200,30') `
        "space=$($pick.space) region=$($pick.region -join ',')"
    Check 'it suggests a paste-ready probe command' `
        ($null -ne $pick -and $pick.probe -eq "KeyMouse probe --hwnd $handleText --region 60,60,200,30") "probe=$($pick.probe)"

    $titleJson = (& $Exe region pick --rect "$($windowRect.L + 40),$($windowRect.T + 5),200,20" --json 2>&1) -join "`n"
    $titlePick = $null
    try { $titlePick = $titleJson | ConvertFrom-Json } catch { }
    Check 'a title-bar selection is reported in window space' `
        ($null -ne $titlePick -and $titlePick.space -eq 'window') "space=$($titlePick.space)"
    Check '...and the suggested command says so' `
        ($null -ne $titlePick -and $titlePick.probe -match '--space window --region 40,5,200,20') "probe=$($titlePick.probe)"

    $null = & $Exe region pick --rect '0,0,0,5' 2>&1
    Check 'a zero-sized rectangle is exit 2' ($LASTEXITCODE -eq 2) "exit=$LASTEXITCODE"
    $null = & $Exe region pick --rect '0,0,10,10' --title 'x' 2>&1
    Check 'a window selector next to a pick is exit 2' ($LASTEXITCODE -eq 2) "exit=$LASTEXITCODE"

    # Regression test for a real bug: with --capture screen, --space window used to start at the
    # client origin, so a title-bar read silently read the client area - both spaces produced
    # byte-identical images. Reaching the same screen pixels through both spaces must match exactly.
    $offsetX = $clientOrigin.X - $windowRect.L
    $offsetY = $clientOrigin.Y - $windowRect.T
    $regionX = 60
    $regionY = [Math]::Max(0, $clientHeight - 120)
    $clientImage = "$env:TEMP\km-space-client.bmp"
    $windowImage = "$env:TEMP\km-space-window.bmp"
    Remove-Item $clientImage, $windowImage -ErrorAction SilentlyContinue
    $null = & $Exe probe @target --region "$regionX,$regionY,200,60" --keep-image $clientImage 2>&1
    $null = & $Exe probe @target --space window --region "$($regionX + $offsetX),$($regionY + $offsetY),200,60" --keep-image $windowImage 2>&1
    $clientHash = if (Test-Path $clientImage) { (Get-FileHash $clientImage -Algorithm SHA256).Hash } else { '' }
    $windowHash = if (Test-Path $windowImage) { (Get-FileHash $windowImage -Algorithm SHA256).Hash } else { '' }
    Check 'client space and window space address the same screen pixels' `
        ($clientHash -ne '' -and $clientHash -eq $windowHash) "client=$clientHash window=$windowHash"

    # The overlay itself, driven by KeyMouse's own input. It covers the screen and is topmost, so
    # the drag lands on the overlay rather than on the target underneath it.
    $null = & $Exe window focus @target

    $dragFile = "$env:TEMP\km-region-drag.json"
    Remove-Item $dragFile -ErrorAction SilentlyContinue
    $picker = Start-Process -FilePath $Exe -ArgumentList 'region', 'pick', '--json' `
        -RedirectStandardOutput $dragFile -RedirectStandardError "$dragFile.err" -NoNewWindow -PassThru
    Start-Sleep -Milliseconds 1500
    $null = & $Exe mouse drag ($clientOrigin.X + 60) ($clientOrigin.Y + 200) ($clientOrigin.X + 260) ($clientOrigin.Y + 230) --duration 200 --steps 8
    $null = $picker.WaitForExit(10000)
    $dragged = $null
    if (Test-Path $dragFile) { try { $dragged = (Get-Content $dragFile -Raw) | ConvertFrom-Json } catch { } }
    Check 'a dragged rectangle comes back as a client-space region' `
        ($picker.HasExited -and $null -ne $dragged -and $dragged.space -eq 'client' -and ($dragged.region -join ',') -eq '60,200,200,30') `
        "exited=$($picker.HasExited) region=$($dragged.region -join ',')"
    if (-not $picker.HasExited) { $picker.Kill() }

    $clickFile = "$env:TEMP\km-region-click.json"
    Remove-Item $clickFile -ErrorAction SilentlyContinue
    $picker = Start-Process -FilePath $Exe -ArgumentList 'region', 'pick', '--json' `
        -RedirectStandardOutput $clickFile -RedirectStandardError "$clickFile.err" -NoNewWindow -PassThru
    Start-Sleep -Milliseconds 1500
    $null = & $Exe mouse click left -x ($clientOrigin.X + 300) -y ($clientOrigin.Y + 300)
    $null = $picker.WaitForExit(10000)
    $clicked = $null
    if (Test-Path $clickFile) { try { $clicked = (Get-Content $clickFile -Raw) | ConvertFrom-Json } catch { } }
    Check 'a click selects the whole client area' `
        ($picker.HasExited -and $null -ne $clicked -and ($clicked.region -join ',') -eq "0,0,$clientWidth,$clientHeight") `
        "exited=$($picker.HasExited) region=$($clicked.region -join ',')"
    if (-not $picker.HasExited) { $picker.Kill() }

    $escFile = "$env:TEMP\km-region-esc.json"
    Remove-Item $escFile -ErrorAction SilentlyContinue
    $picker = Start-Process -FilePath $Exe -ArgumentList 'region', 'pick', '--json' `
        -RedirectStandardOutput $escFile -RedirectStandardError "$escFile.err" -NoNewWindow -PassThru
    Start-Sleep -Milliseconds 1500
    $null = & $Exe key press esc
    $null = $picker.WaitForExit(10000)
    Check 'ESC cancels the picker with exit 3' `
        ($picker.HasExited -and $picker.ExitCode -eq 3) "exited=$($picker.HasExited) exit=$($picker.ExitCode)"
    if (-not $picker.HasExited) { $picker.Kill() }
}
finally {
    Get-Process KeyMouse.SmokeTarget -ErrorAction SilentlyContinue | Stop-Process -Force
    if ($null -ne $savedClipboard) { Set-Clipboard -Value $savedClipboard }
}

Write-Host "`n$script:passed passed, $script:failed failed"
exit ([int]($script:failed -gt 0))
