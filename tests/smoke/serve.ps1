#requires -Version 7
<#
    冒烟模块：serve。由 tests\smoke.ps1 载入（pwsh tests\smoke.ps1 -Only serve 只跑这一个）。
    依赖 common.ps1 提供的：$Exe / $target / $targetInScript / Check。
#>

    # ------------------------------------------------------------------ resident runner (serve + IPC)
    # The architecture in one check: a long-lived Runner serves the editor over a named pipe, and the
    # CLI is a second entry point into that same service instead of doing the work itself.
    Write-Host "`n-- serve: the resident Runner, driven from the CLI --"
    $serveFlow = Join-Path $env:TEMP "km-smoke-serve-$PID.json"
    Set-Content -LiteralPath $serveFlow -Encoding utf8 `
        -Value '{"format":"keymouse-flow","version":1,"steps":[{"type":"sleep","ms":30},{"type":"sleep","ms":30}]}'
    $serve = Start-Process -FilePath $Exe -ArgumentList 'serve' -NoNewWindow -PassThru
    Start-Sleep -Milliseconds 1800
    Check 'the runner starts and stays up' (-not $serve.HasExited) 'serve exited immediately'

    $status = & $Exe runner status 2>&1
    Check 'the CLI can ask the resident runner for status' ($LASTEXITCODE -eq 0) "exit=$LASTEXITCODE :: $($status -join ' / ')"
    Check '...and it answers with its version and pipe name' `
        ((($status -join ' ') -match '\d+\.\d+\.\d+') -and (($status -join ' ') -match 'keymouse-runner')) ($status -join ' / ')

    $through = & $Exe runner run $serveFlow --dry-run 2>&1
    Check 'a flow can be executed through the service' ($LASTEXITCODE -eq 0) "exit=$LASTEXITCODE :: $($through -join ' / ')"
    Check '...with each step reported as it starts' `
        ((($through -join ' ') -match '\[1/2\]') -and (($through -join ' ') -match '\[2/2\]')) ($through -join ' / ')

    $after = & $Exe runner status 2>&1
    Check 'the finished job is listed with its exit code' (($after -join ' ') -match 'finished') ($after -join ' / ')

    $null = & $Exe runner stop 2>&1
    Start-Sleep -Milliseconds 1200
    Check 'the runner stops when asked' ($serve.HasExited) 'still running'
    if (-not $serve.HasExited) { $serve.Kill() }
    Remove-Item $serveFlow -ErrorAction SilentlyContinue
