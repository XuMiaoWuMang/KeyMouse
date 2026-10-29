#requires -Version 7
<#
    冒烟模块：loops。由 tests\smoke.ps1 载入（pwsh tests\smoke.ps1 -Only loops 只跑这一个）。
    依赖 common.ps1 提供的：$Exe / $target / $targetInScript / Check。
#>

    Write-Host "`n== loops, and one process from start to finish =="
    # 先把光标放进靶子的文本框：`window focus` 只把窗口提到前台，不保证里面的控件拿到键盘焦点。
    # 上一次全跑时这个模块的键盘全落空（文档里还是 features 模块留下的字），而单独敲 ctrl+a/c 又能成——
    # 差别就在"谁拿着插入符"。点一下正文区域，后面的事才有意义。
    $null = & $Exe mouse click left -wx 120 -wy 60 @target
    Start-Sleep -Milliseconds 250
    $loopScript = Join-Path $RunDir 'keymouse-smoke-loop.txt'
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
    $loopReport = Join-Path $RunDir 'keymouse-smoke-loop-report.json'
    Remove-Item $loopReport -Force -ErrorAction SilentlyContinue
    # -NoNewWindow 是必须的：不带它 Start-Process 会走 ShellExecute，这台机器上会弹
    # "打开文件 - 安全警告（无法验证发布者）" 把整轮冒烟卡在那儿等人点。
    $proc = Start-Process -FilePath $Exe -ArgumentList 'run', "`"$loopScript`"", '--report', "`"$loopReport`"" -NoNewWindow -PassThru
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
    # 上一次全跑时这两条红了，读到的却是上一个模块（features）留在靶子里的文字：
    # 说明"清空 + 循环里的键盘"一个字都没到靶子，而不是循环写错了内容。把靶子里的内容
    # 和报告一起打出来，下次红的时候能直接看出是"没清掉"还是"没敲进去"。
    $loopDiagnostics = ''
    if ($looped -notmatch 'loop line 0' -and (Test-Path $loopReport)) {
        try {
            $report = Get-Content $loopReport -Raw | ConvertFrom-Json
            $loopDiagnostics = ' :: ' + (($report.commands | Select-Object -First 6 |
                ForEach-Object { "$($_.command)=$($_.exitCode)" }) -join ' ')
        } catch { }
    }
    Check 'the loop wrote its first iteration' ($looped -match 'loop line 0') "got [$looped]$loopDiagnostics"
    Check 'the loop wrote its last iteration' ($looped -match 'loop line 3') "got [$looped]$loopDiagnostics"
    Check 'the loop stopped at the requested count' ($looped -notmatch 'loop line 4') "got [$looped]$loopDiagnostics"

    # Structure is checked in full before the first command, so a broken loop sends nothing.
    @('repeat 3', 'key press f24') | Set-Content -Path $loopScript -Encoding utf8
    $null = & $Exe run $loopScript 2>&1
    Check 'a repeat without end is refused before anything runs' ($LASTEXITCODE -eq 2) "exit=$LASTEXITCODE"

    @('key press f24', 'end') | Set-Content -Path $loopScript -Encoding utf8
    $null = & $Exe run $loopScript 2>&1
    Check 'a stray end is refused before anything runs' ($LASTEXITCODE -eq 2) "exit=$LASTEXITCODE"
    Remove-Item $loopScript -Force -ErrorAction SilentlyContinue

