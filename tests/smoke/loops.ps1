#requires -Version 7
<#
    冒烟模块：loops。由 tests\smoke.ps1 载入（pwsh tests\smoke.ps1 -Only loops 只跑这一个）。
    依赖 common.ps1 提供的：$Exe / $target / $targetInScript / Check。
#>

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

