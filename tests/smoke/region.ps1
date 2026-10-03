#requires -Version 7
<#
    冒烟模块：region。由 tests\smoke.ps1 载入（pwsh tests\smoke.ps1 -Only region 只跑这一个）。
    依赖 common.ps1 提供的：$Exe / $target / $targetInScript / Check。
#>

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
        # Park the caret well below the region: it blinks, so two captures can disagree (exit 6), and a
        # frame with the caret inside the region can make the engine misread the whole line. Measured:
        # '你好，世界|' reads as 'Re, Hh' where the same pixels without the caret read correctly.
        $null = & $Exe key press enter -n 3 @target
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
    $null = Wait-ForWindow 'KeyMouse 选区'
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
    $null = Wait-ForWindow 'KeyMouse 选区'
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
    $null = Wait-ForWindow 'KeyMouse 选区'
    $null = & $Exe key press esc
    $null = $picker.WaitForExit(10000)
    Check 'ESC cancels the picker with exit 3' `
        ($picker.HasExited -and $picker.ExitCode -eq 3) "exited=$($picker.HasExited) exit=$($picker.ExitCode)"
    if (-not $picker.HasExited) { $picker.Kill() }
