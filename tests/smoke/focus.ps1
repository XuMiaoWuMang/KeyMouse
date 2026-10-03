#requires -Version 7
<#
    冒烟模块：focus（聚焦与还原）。由 tests\smoke.ps1 载入（pwsh tests\smoke.ps1 -Only focus 只跑这一个）。
    依赖 common.ps1 提供的：$Exe / $target / Check。

    为什么单独立一个模块：抢前台是 Windows 上最容易"看起来能用、实际被拒"的一件事。这里用真窗口验证两件事：
    1) 没说允许，流程**不还原**最小化窗口（退出码 4，信息里告诉你怎么允许）；
    2) 说了允许，流程能还原 + 聚焦，并且**点击真的发得出去**（聚焦校验通过才会发送）。
#>

    Write-Host "`n-- 聚焦与还原（最小化窗口）--"

    # 最小化是"测试夹具"用的 Win32 调用：产品自己不会去最小化别人的窗口。
    Add-Type -Namespace KmSmoke -Name Native -MemberDefinition '[DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);'

    $handleLine = & $Exe window list --process KeyMouse.SmokeTarget 2>&1 | Where-Object { $_ -match '^0x' } | Select-Object -First 1
    $handle = [IntPtr]([Convert]::ToInt64((($handleLine -split '\s+')[0]), 16))
    $null = [KmSmoke.Native]::ShowWindow($handle, 6)   # SW_MINIMIZE
    Start-Sleep -Milliseconds 700
    $minimized = (& $Exe window list --process KeyMouse.SmokeTarget 2>&1 | Out-String)
    Check 'the target can be minimized for this test' ($minimized -match '最小化') "list=[$(($minimized -replace '\s+', ' ').Trim())]"

    $focusDir = Join-Path $env:TEMP "km-smoke-focus-$PID"
    New-Item -ItemType Directory -Force -Path $focusDir | Out-Null
    $steps = '{"type":"focus","target":{"process":"KeyMouse.SmokeTarget"}},' +
        '{"type":"click","at":{"space":"client","x":100,"y":300},"button":"left","target":{"process":"KeyMouse.SmokeTarget"}}'
    Set-Content -LiteralPath (Join-Path $focusDir 'no-restore.json') -Encoding utf8 `
        -Value ('{"format":"keymouse-flow","version":1,"steps":[' + $steps + ']}')
    Set-Content -LiteralPath (Join-Path $focusDir 'restore.json') -Encoding utf8 `
        -Value ('{"format":"keymouse-flow","version":1,"allowRestore":true,"steps":[' + $steps + ']}')

    $refused = & $Exe run (Join-Path $focusDir 'no-restore.json') 2>&1
    Check 'a flow does not restore a minimized window unless it says so' ($LASTEXITCODE -eq 4) "exit=$LASTEXITCODE :: $($refused -join ' / ')"
    # 运行日志里的步骤输出会被截短，所以这条断言直接问 CLI 要原话。
    $direct = & $Exe window focus --process KeyMouse.SmokeTarget 2>&1 | Out-String
    Check '...and the refusal says how to allow it' ($direct -match 'allow-restore') "message=$(($direct -replace '\s+', ' ').Trim())"

    $restored = & $Exe run (Join-Path $focusDir 'restore.json') --echo 2>&1
    Check '...and restores, focuses and clicks when the document allows it' ($LASTEXITCODE -eq 0) "exit=$LASTEXITCODE :: $($restored -join ' / ')"
    $after = (& $Exe window list --process KeyMouse.SmokeTarget 2>&1 | Out-String)
    Check '...leaving the window visible again' ($after -notmatch '最小化') "list=[$(($after -replace '\s+', ' ').Trim())]"
    $inspect = (& $Exe window inspect --process KeyMouse.SmokeTarget 2>&1 | Out-String)
    Check '...and in the foreground, which is what the click was gated on' `
        ($inspect -match '前台=True') "inspect=[$(($inspect -replace '\s+', ' ').Trim())]"

    Remove-Item $focusDir -Recurse -Force -ErrorAction SilentlyContinue
