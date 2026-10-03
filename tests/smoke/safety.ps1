#requires -Version 7
<#
    冒烟模块：safety。由 tests\smoke.ps1 载入（pwsh tests\smoke.ps1 -Only safety 只跑这一个）。
    依赖 common.ps1 提供的：$Exe / $target / $targetInScript / Check。
#>

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

