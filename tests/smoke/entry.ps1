#requires -Version 7
<#
    冒烟模块：entry。由 tests\smoke.ps1 载入（pwsh tests\smoke.ps1 -Only entry 只跑这一个）。
    依赖 common.ps1 提供的：$Exe / $target / $targetInScript / Check。
#>

    Write-Host '== exit codes =='
    $null = & $Exe mouse click left --title 'no-such-window-xyz' 2>&1
    Check 'unknown selector exits 3' ($LASTEXITCODE -eq 3) "exit=$LASTEXITCODE"

    $null = & $Exe mouse move abc 2>&1
    Check 'bad argument exits 2' ($LASTEXITCODE -eq 2) "exit=$LASTEXITCODE"

    $null = & $Exe key press nosuchkey 2>&1
    Check 'unknown key exits 2' ($LASTEXITCODE -eq 2) "exit=$LASTEXITCODE"

    $null = & $Exe key press f24 --focus-policy none --title 'no-such-window-xyz' 2>&1
    Check 'unresolvable target exits 3' ($LASTEXITCODE -eq 3) "exit=$LASTEXITCODE"

    # 靶子由 common.ps1 启动（任何模块都需要它），这里只断言它确实起来了。
    Write-Host "`n== the smoke target =="
    $null = & $Exe window focus @target 2>&1
    Check 'the target window is up and can be focused' ($LASTEXITCODE -eq 0) "exit=$LASTEXITCODE"
