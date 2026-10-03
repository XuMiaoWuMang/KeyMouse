#requires -Version 7
<#
    Archives the perception measurements so a human can look at them afterwards.

    Every run gets its own timestamped folder under tests/evidence/, and inside it:

        commands.txt   every command line with its exit code, stdout and stderr
        json/          the raw JSON of every call, plus the .err.txt of the failures
        images/        every screenshot, converted from BMP to PNG for viewing
        summary.md     the numbers, in tables, plus an index of the files

    Reads are retried the way a caller would retry them: exit code 6 means "did not see it"
    (two captures disagreed, usually a frame caught mid-repaint) and the tool documents
    3/4/5/6 as safe to retry, so the summary reports both the first try and the retried
    outcome. Nothing is thrown away: the JSON and the screenshot of every attempt are kept.

    It drives the test's own smoke target, so it touches nothing the user owns.

        pwsh tests/evidence.ps1
        pwsh tests/evidence.ps1 -Samples 10 -Attempts 3
#>
[CmdletBinding()]
param(
    [string]$Exe = (Join-Path $PSScriptRoot '..\dist\KeyMouse.exe'),
    [string]$TargetExe = (Join-Path $PSScriptRoot 'KeyMouse.SmokeTarget\bin\Release\net10.0-windows\KeyMouse.SmokeTarget.exe'),
    [int]$Samples = 20,
    [int]$Attempts = 3
)

$ErrorActionPreference = 'Stop'
$exe = (Resolve-Path $Exe).Path
$targetExe = (Resolve-Path $TargetExe).Path
$targetTitle = 'KeyMouse 冒烟靶子'

$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$run = Join-Path $PSScriptRoot "evidence\$stamp"
$imageDir = Join-Path $run 'images'
$jsonDir = Join-Path $run 'json'
New-Item -ItemType Directory -Force -Path $imageDir, $jsonDir | Out-Null
$commandLog = Join-Path $run 'commands.txt'

Add-Type -AssemblyName System.Drawing
Add-Type -Namespace Evidence -Name Win -MemberDefinition @'
[DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
[DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr h, out RECT r);
[DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr h, ref POINT p);
[StructLayout(LayoutKind.Sequential)] public struct RECT { public int L; public int T; public int R; public int B; }
[StructLayout(LayoutKind.Sequential)] public struct POINT { public int X; public int Y; }
'@

function Write-Log([string]$line = '') { Add-Content -LiteralPath $commandLog -Value $line -Encoding utf8 }

function Invoke-Tool {
    param([string[]]$Arguments, [string]$JsonName, [switch]$WantJson)
    Write-Log ('$ KeyMouse ' + ($Arguments -join ' '))
    # Direct invocation, not Start-Process: -ArgumentList joins an array with spaces and loses the
    # quoting, so "--title KeyMouse 冒烟靶子" would arrive as two arguments. stderr goes to a file
    # because a JSON run has to keep stdout clean for ConvertFrom-Json.
    $stderrFile = Join-Path $env:TEMP "ev-stderr-$([guid]::NewGuid().ToString('N')).txt"
    $stdoutLines = & $exe @Arguments 2>$stderrFile
    $code = $LASTEXITCODE
    $stdout = ($stdoutLines | Out-String)
    $stderr = if (Test-Path $stderrFile) { Get-Content $stderrFile -Raw } else { '' }
    Remove-Item $stderrFile -ErrorAction SilentlyContinue
    if ($null -eq $stdout) { $stdout = '' }
    if ($null -eq $stderr) { $stderr = '' }

    Write-Log "  exit=$code"
    if ($stdout.Trim().Length -gt 0) { Write-Log ('  [stdout] ' + ($stdout.Trim() -replace "`r?`n", "`n           ")) }
    if ($stderr.Trim().Length -gt 0) { Write-Log ('  [stderr] ' + ($stderr.Trim() -replace "`r?`n", "`n           ")) }
    Write-Log ''

    if ($JsonName) {
        if ($WantJson -and $stdout.Trim().Length -gt 0) {
            Set-Content -LiteralPath (Join-Path $jsonDir "$JsonName.json") -Value $stdout -Encoding utf8
        }
        if ($stderr.Trim().Length -gt 0) {
            Set-Content -LiteralPath (Join-Path $jsonDir "$JsonName.err.txt") -Value $stderr -Encoding utf8
        }
    }

    $json = $null
    try { $json = $stdout | ConvertFrom-Json } catch { }
    return [pscustomobject]@{ Exit = $code; Stdout = $stdout; Stderr = $stderr; Json = $json }
}

function Save-Image {
    param([string]$BmpPath, [string]$Name)
    if (-not (Test-Path -LiteralPath $BmpPath)) { return $null }
    $png = Join-Path $imageDir "$Name.png"
    $image = [System.Drawing.Image]::FromFile($BmpPath)
    try { $image.Save($png, [System.Drawing.Imaging.ImageFormat]::Png) }
    finally { $image.Dispose() }
    Remove-Item -LiteralPath $BmpPath -ErrorAction SilentlyContinue
    return $Name
}

function Get-BmpSize([string]$path) {
    if (-not (Test-Path -LiteralPath $path)) { return '' }
    $bytes = [System.IO.File]::ReadAllBytes($path)
    if ($bytes.Length -lt 26) { return '' }
    return ('{0}x{1}' -f [BitConverter]::ToInt32($bytes, 18), [Math]::Abs([BitConverter]::ToInt32($bytes, 22)))
}

function Get-Read([object]$json) {
    if ($null -eq $json) { return '' }
    return (((($json.lines | ForEach-Object { $_.text }) -join '') -replace '\s', '') -replace '\|+$', '')
}

$sampleTexts = @(
    '你好，世界'
    'ProbeCheck12345'
    '请输入文件名后点击保存按钮'
    '保存 取消 确定'
    'Order #4471 shipped'
    '区域太小会出现重影'
    '文件 编辑 查看 H1'
    '新建 文本文档.txt'
    '247 * 119'
    '圆角 阴影 重影'
    'error: 找不到文件 (0x2)'
    '用户名或密码不正确'
    'KeyMouse v2.0.0 已就绪'
    '导出为 PNG/JPG，质量 85%'
    '第 3 步：确认后点击"下一步"'
    'C:\Users\xumiao\Desktop\报告.docx'
    '总计 1,234.56 元'
    '是否保存更改？[是] [否] [取消]'
    '连接超时，请重试（第 2/5 次）'
    'Ctrl+S 保存，Ctrl+Z 撤销'
) | Select-Object -First $Samples

$configs = [ordered]@{
    'default'          = @()
    'default+normalize' = @('--normalize')
    'nearest-3x'       = @('--scale', '3', '--pad', '16', '--resample', 'nearest')
    'bilinear-3x'      = @('--scale', '3', '--pad', '16', '--resample', 'bilinear')
    'bilinear-3x+normalize' = @('--scale', '3', '--pad', '16', '--resample', 'bilinear', '--normalize')
}

$firstExact = @{}; $eventualExact = @{}; $confSum = @{}; $confCount = @{}; $msSum = @{}; $exitSix = @{}
foreach ($key in $configs.Keys) {
    $firstExact[$key] = 0; $eventualExact[$key] = 0; $confSum[$key] = 0.0; $confCount[$key] = 0
    $msSum[$key] = 0; $exitSix[$key] = 0
}
$rows = @()

Get-Process KeyMouse.SmokeTarget -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Process -FilePath $targetExe | Out-Null
Start-Sleep -Milliseconds 1400

try {
    Write-Log '== environment =='
    Write-Log ('date       ' + (Get-Date -Format 'yyyy-MM-dd HH:mm:ss zzz'))
    Write-Log ("exe        $exe")
    Write-Log ("pwsh       " + $PSVersionTable.PSVersion)
    $null = Invoke-Tool -Arguments @('--version')
    $null = Invoke-Tool -Arguments @('window', 'inspect', '--class', 'Progman')
    $null = Invoke-Tool -Arguments @('window', 'inspect', '--title', $targetTitle)

    $target = Get-Process KeyMouse.SmokeTarget | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
    $handle = $target.MainWindowHandle
    $windowRect = New-Object Evidence.Win+RECT
    $null = [Evidence.Win]::GetWindowRect($handle, [ref]$windowRect)
    $clientOrigin = New-Object Evidence.Win+POINT
    $clientOrigin.X = 0; $clientOrigin.Y = 0
    $null = [Evidence.Win]::ClientToScreen($handle, [ref]$clientOrigin)

    $null = Invoke-Tool -Arguments @('window', 'focus', '--title', $targetTitle)

    # ---------------------------------------------------------------- caret
    Write-Log '== the caret inside the region (why the region must exclude it) =='
    $null = Invoke-Tool -Arguments @('key', 'combo', 'ctrl+a', '--title', $targetTitle)
    $null = Invoke-Tool -Arguments @('key', 'press', 'delete', '--title', $targetTitle)
    $null = Invoke-Tool -Arguments @('key', 'type', '你好，世界', '--title', $targetTitle)
    Start-Sleep -Milliseconds 400
    $caretVariants = @()
    for ($i = 0; $i -lt 8 -and $caretVariants.Count -lt 2; $i++) {
        $bmp = Join-Path $env:TEMP "ev-caret-$i.bmp"
        Remove-Item $bmp -ErrorAction SilentlyContinue
        $shot = Invoke-Tool -Arguments @('probe', '--title', $targetTitle, '--region', '0,0,400,32', '--reads', '1',
            '--keep-image', $bmp, '--json') -JsonName "caret-try$($i + 1)" -WantJson
        if (-not (Test-Path $bmp)) { continue }
        $hash = (Get-FileHash $bmp -Algorithm SHA256).Hash
        if ($caretVariants | Where-Object { $_.Hash -eq $hash }) { continue }
        $png = Save-Image -BmpPath $bmp -Name "caret-frame$($caretVariants.Count + 1)"
        $caretVariants += [pscustomobject]@{
            Hash = $hash; Text = (Get-Read $shot.Json); Conf = $shot.Json.confidence; Png = $png }
    }
    $null = Invoke-Tool -Arguments @('key', 'press', 'enter', '-n', '3', '--title', $targetTitle)
    Start-Sleep -Milliseconds 300
    $caretAfterEnter = Invoke-Tool -Arguments @('probe', '--title', $targetTitle, '--region', '0,0,400,32', '--json') `
        -JsonName 'caret-after-enter' -WantJson

    # ---------------------------------------------------------------- sampling
    Write-Log "== upscale sampling: $($sampleTexts.Count) samples x $($configs.Count) configs x up to $Attempts attempts =="
    $index = 0
    foreach ($sample in $sampleTexts) {
        $index++
        $null = Invoke-Tool -Arguments @('key', 'combo', 'ctrl+a', '--title', $targetTitle)
        $null = Invoke-Tool -Arguments @('key', 'press', 'delete', '--title', $targetTitle)
        $null = Invoke-Tool -Arguments @('key', 'type', $sample, '--title', $targetTitle)
        # The caret has to leave the region: it blinks, it makes the two captures disagree (exit 6)
        # and - measured below - it makes Tesseract misread the whole line. Three newlines put it
        # well below the 32 pixel band that is being read.
        $null = Invoke-Tool -Arguments @('key', 'press', 'enter', '-n', '3', '--title', $targetTitle)
        Start-Sleep -Milliseconds 400

        $expected = $sample -replace '\s', ''
        foreach ($key in $configs.Keys) {
            $tag = '{0:d2}-{1}' -f $index, $key
            $firstOk = $false; $ok = $false; $tries = 0; $read = ''; $lastExit = $null
            $images = @()
            for ($attempt = 1; $attempt -le $Attempts; $attempt++) {
                $tries = $attempt
                $rawBmp = Join-Path $env:TEMP "ev-$tag-try$attempt-raw.bmp"
                $preparedBmp = Join-Path $env:TEMP "ev-$tag-try$attempt-prepared.bmp"
                Remove-Item $rawBmp, $preparedBmp -ErrorAction SilentlyContinue
                $suffix = if ($Attempts -gt 1) { "-try$attempt" } else { '' }

                $arguments = @('probe', '--title', $targetTitle, '--region', '0,0,400,32') + $configs[$key] +
                             @('--json', '--keep-image', $rawBmp, '--keep-prepared', $preparedBmp)
                $result = Invoke-Tool -Arguments $arguments -JsonName "sampling-$tag$suffix" -WantJson
                $lastExit = $result.Exit
                $read = Get-Read $result.Json
                $rawSize = Get-BmpSize $rawBmp
                $preparedSize = Get-BmpSize $preparedBmp
                $rawPng = Save-Image -BmpPath $rawBmp -Name "$tag-try$attempt-raw"
                $preparedPng = Save-Image -BmpPath $preparedBmp -Name "$tag-try$attempt-prepared"
                $images += [pscustomobject]@{ Try = $attempt; Exit = $result.Exit; Read = $read
                    Raw = $rawPng; RawSize = $rawSize; Prepared = $preparedPng; PreparedSize = $preparedSize }

                if ($result.Exit -eq 6) { $exitSix[$key]++ }
                if ($result.Exit -eq 0 -and $read -eq $expected) {
                    if ($attempt -eq 1) { $firstOk = $true }
                    $ok = $true
                    $confSum[$key] += [double]$result.Json.confidence
                    $confCount[$key]++
                    $msSum[$key] += [int]$result.Json.elapsedMs
                    break
                }
            }
            if ($firstOk) { $firstExact[$key]++ }
            if ($ok) { $eventualExact[$key]++ }
            $rows += [pscustomobject]@{ Sample = $sample; Config = $key; First = $firstOk; Ok = $ok
                Tries = $tries; Exit = $lastExit; Read = $read; Images = $images }
        }
    }

    # ---------------------------------------------------------------- languages
    Write-Log '== who decides Chinese vs English (the engine, not us) =='
    $null = Invoke-Tool -Arguments @('key', 'combo', 'ctrl+a', '--title', $targetTitle)
    $null = Invoke-Tool -Arguments @('key', 'press', 'delete', '--title', $targetTitle)
    $null = Invoke-Tool -Arguments @('key', 'type', '你好abc世界123 ABC', '--title', $targetTitle)
    $null = Invoke-Tool -Arguments @('key', 'press', 'enter', '-n', '3', '--title', $targetTitle)
    Start-Sleep -Milliseconds 400
    $languageRows = @()
    foreach ($lang in @('eng', 'chi_sim', 'eng+chi_sim')) {
        $tag = $lang -replace '\+', '-'
        $bmp = Join-Path $env:TEMP "ev-lang-$tag.bmp"
        Remove-Item $bmp -ErrorAction SilentlyContinue
        $result = Invoke-Tool -Arguments @('probe', '--title', $targetTitle, '--region', '0,0,400,32', '--lang', $lang,
            '--keep-image', $bmp, '--json') -JsonName "language-$tag" -WantJson
        $png = Save-Image -BmpPath $bmp -Name "language-$tag-raw"
        $languageRows += [pscustomobject]@{
            Lang = $lang; Exit = $result.Exit; Text = (Get-Read $result.Json)
            Conf = $result.Json.confidence; Engine = $result.Json.engine.languages; Png = $png }
    }

    # ---------------------------------------------------------------- region picker
    Write-Log '== region pick =='
    $pickClient = Invoke-Tool -Arguments @('region', 'pick', '--rect',
        "$($clientOrigin.X + 60),$($clientOrigin.Y + 60),200,30", '--json') -JsonName 'region-pick-client' -WantJson
    $pickWindow = Invoke-Tool -Arguments @('region', 'pick', '--rect',
        "$($windowRect.L + 40),$($windowRect.T + 5),200,20", '--json') -JsonName 'region-pick-window' -WantJson
    $pickBad = Invoke-Tool -Arguments @('region', 'pick', '--rect', '0,0,0,5') -JsonName 'region-pick-bad-rect'

    $overlayBmp = Join-Path $env:TEMP 'ev-overlay.bmp'
    Remove-Item $overlayBmp -ErrorAction SilentlyContinue
    $dragFile = Join-Path $jsonDir 'region-pick-drag.json'
    $picker = Start-Process -FilePath $exe -ArgumentList 'region', 'pick', '--json' `
        -RedirectStandardOutput $dragFile -RedirectStandardError (Join-Path $jsonDir 'region-pick-drag.err.txt') -NoNewWindow -PassThru
    Start-Sleep -Milliseconds 1500
    Write-Log '$ (overlay is open; probe takes a screenshot of it)'
    $overlayShot = Invoke-Tool -Arguments @('probe', '--title', 'KeyMouse 选区', '--scale', '1', '--pad', '0',
        '--reads', '1', '--keep-image', $overlayBmp, '--json') -JsonName 'region-overlay-screenshot' -WantJson
    $overlayPng = Save-Image -BmpPath $overlayBmp -Name 'region-picker-overlay'

    # The overlay is the one dark background we control: white text on a dark band. It is where the
    # question "does raw pixels still read light-on-dark" gets a reproducible answer.
    $darkRows = @()
    foreach ($darkName in 'raw', 'normalized') {
        $darkFlag = if ($darkName -eq 'normalized') { @('--normalize') } else { @() }
        $darkBmp = Join-Path $env:TEMP "ev-dark-$darkName.bmp"
        Remove-Item $darkBmp -ErrorAction SilentlyContinue
        $darkArguments = @('probe', '--title', 'KeyMouse 选区', '--region', '1000,10,700,40') + $darkFlag +
                         @('--keep-image', $darkBmp, '--json')
        $darkRead = Invoke-Tool -Arguments $darkArguments -JsonName "dark-$darkName" -WantJson
        $darkPng = Save-Image -BmpPath $darkBmp -Name "dark-$darkName-raw"
        $darkRows += [pscustomobject]@{ Mode = $darkName; Exit = $darkRead.Exit
            Text = (Get-Read $darkRead.Json); Conf = $darkRead.Json.confidence; Png = $darkPng }
    }
    $null = Invoke-Tool -Arguments @('mouse', 'drag',
        ($clientOrigin.X + 60), ($clientOrigin.Y + 200), ($clientOrigin.X + 260), ($clientOrigin.Y + 230),
        '--duration', '250', '--steps', '8')
    $null = $picker.WaitForExit(10000)
    if (-not $picker.HasExited) { $picker.Kill() }
    $dragExit = $picker.ExitCode
    $dragJson = $null
    if (Test-Path $dragFile) { try { $dragJson = (Get-Content $dragFile -Raw) | ConvertFrom-Json } catch { } }

    $escFile = Join-Path $jsonDir 'region-pick-esc.json'
    $picker = Start-Process -FilePath $exe -ArgumentList 'region', 'pick', '--json' `
        -RedirectStandardOutput $escFile -RedirectStandardError (Join-Path $jsonDir 'region-pick-esc.err.txt') -NoNewWindow -PassThru
    Start-Sleep -Milliseconds 1500
    $null = Invoke-Tool -Arguments @('key', 'press', 'esc')
    $null = $picker.WaitForExit(10000)
    if (-not $picker.HasExited) { $picker.Kill() }
    $escExit = $picker.ExitCode

    # ---------------------------------------------------------------- coordinate spaces
    Write-Log '== coordinate spaces (the same screen pixels reached two ways) =='
    $clientBmp = Join-Path $env:TEMP 'ev-space-client.bmp'
    $windowBmp = Join-Path $env:TEMP 'ev-space-window.bmp'
    Remove-Item $clientBmp, $windowBmp -ErrorAction SilentlyContinue
    $null = Invoke-Tool -Arguments @('probe', '--title', $targetTitle, '--region', '60,300,200,60',
        '--keep-image', $clientBmp, '--json') -JsonName 'space-client' -WantJson
    $offsetX = $clientOrigin.X - $windowRect.L
    $offsetY = $clientOrigin.Y - $windowRect.T
    $null = Invoke-Tool -Arguments @('probe', '--title', $targetTitle, '--space', 'window', '--region',
        "$(60 + $offsetX),$(300 + $offsetY),200,60", '--keep-image', $windowBmp, '--json') -JsonName 'space-window' -WantJson
    $clientHash = if (Test-Path $clientBmp) { (Get-FileHash $clientBmp -Algorithm SHA256).Hash } else { '' }
    $windowHash = if (Test-Path $windowBmp) { (Get-FileHash $windowBmp -Algorithm SHA256).Hash } else { '' }
    $clientPng = Save-Image -BmpPath $clientBmp -Name 'space-client-raw'
    $windowPng = Save-Image -BmpPath $windowBmp -Name 'space-window-raw'

    Write-Log '== title bar (window space) =='
    $titleBmp = Join-Path $env:TEMP 'ev-title.bmp'
    Remove-Item $titleBmp -ErrorAction SilentlyContinue
    $titleRead = Invoke-Tool -Arguments @('probe', '--title', $targetTitle, '--space', 'window', '--region', '0,0,600,32',
        '--keep-image', $titleBmp, '--json') -JsonName 'title-bar' -WantJson
    $titlePng = Save-Image -BmpPath $titleBmp -Name 'title-bar-raw'

    # ---------------------------------------------------------------- summary
    $lines = New-Object System.Collections.Generic.List[string]
    $lines.Add("# KeyMouse evidence $stamp")
    $lines.Add('')
    $lines.Add("Command: ``pwsh tests/evidence.ps1 -Samples $($sampleTexts.Count) -Attempts $Attempts``")
    $lines.Add("Machine: $env:COMPUTERNAME, PowerShell $($PSVersionTable.PSVersion)")
    $lines.Add('')
    $lines.Add('## Upscale sampling')
    $lines.Add('')
    $lines.Add('Text is typed into the smoke target, read back from region 0,0,400,32 and compared character for character.')
    $lines.Add("A read that exits 6 (two captures disagreed) is retried, up to $Attempts attempts, because the tool documents 6 as safe to retry.")
    $lines.Add('')
    $lines.Add('| config | exact on first try | exact within retries | exit 6 during the run | avg conf (successful reads) | avg ms |')
    $lines.Add('| --- | --- | --- | --- | --- | --- |')
    foreach ($key in $configs.Keys) {
        $averageConf = if ($confCount[$key] -gt 0) { '{0:N1}' -f ($confSum[$key] / $confCount[$key]) } else { '-' }
        $averageMs = if ($confCount[$key] -gt 0) { '{0:N0}' -f ($msSum[$key] / $confCount[$key]) } else { '-' }
        $lines.Add(('| {0} | {1}/{2} | {3}/{2} | {4} | {5} | {6} |' -f $key, $firstExact[$key], $sampleTexts.Count,
            $eventualExact[$key], $exitSix[$key], $averageConf, $averageMs))
    }
    $lines.Add('')
    $lines.Add('| # | sample | config | tries | last exit | read back | screenshots (every attempt) |')
    $lines.Add('| --- | --- | --- | --- | --- | --- | --- |')
    $index = 0
    foreach ($row in $rows) {
        if ($row.Config -eq $configs.Keys[0]) { $index++ }
        $images = ($row.Images | ForEach-Object { "``$($_.Raw)`` ({0}) ``$($_.Prepared)`` ({1})" -f $_.RawSize, $_.PreparedSize }) -join '<br>'
        $verdict = if ($row.Ok) { if ($row.First) { 'yes' } else { "yes (try $($row.Tries))" } } else { '**no**' }
        $lines.Add(('| {0} | {1} | {2} | {3} | {4} | {5} {6} | {7} |' -f $index, $row.Sample, $row.Config, $row.Tries,
            $(if ($null -eq $row.Exit) { '' } else { $row.Exit }), ($row.Read -replace '\|', '\|'), $verdict, $images))
    }
    $lines.Add('')
    $lines.Add('## Caret')
    $lines.Add('')
    $lines.Add('The same region, read with the text caret inside it. The caret blinks, so consecutive captures')
    $lines.Add('disagree (exit 6), and - measured - a frame with the caret can make the engine misread the whole line.')
    $lines.Add('')
    $lines.Add('| frame | caret | read back | conf | screenshot |')
    $lines.Add('| --- | --- | --- | --- | --- |')
    $caretIndex = 0
    foreach ($variant in $caretVariants) {
        $caretIndex++
        $hasCaret = if ($variant.Text -match '\|$' -or $variant.Text -eq '') { '?' } else { '?' }
        $lines.Add(('| frame {0} | see screenshot | {1} | {2} | ``images/{3}.png`` |' -f $caretIndex, ($variant.Text -replace '\|', '\|'), $variant.Conf, $variant.Png))
    }
    $lines.Add(('| after three newlines (caret below the band) | no | {0} | {1} | |' -f
        ((Get-Read $caretAfterEnter.Json) -replace '\|', '\|'), $caretAfterEnter.Json.confidence))
    $lines.Add('')
    $lines.Add('A band that is too short clips the glyphs and reads as garbage too: 400x20 over the same line gave')
    $lines.Add('`4eim+s` where 400x32 reads it correctly, so the height has to cover the full line box.')
    $lines.Add('')
    $lines.Add('## Who decides Chinese vs English')
    $lines.Add('')
    $lines.Add('The same mixed line (`你好abc世界123 ABC`) read with three language sets. KeyMouse passes `--lang`')
    $lines.Add('straight to the engine, never classifies a character itself, and echoes the set in the JSON.')
    $lines.Add('')
    $lines.Add('| --lang | engine languages | exit | read back | conf | screenshot |')
    $lines.Add('| --- | --- | --- | --- | --- | --- |')
    foreach ($row in $languageRows) {
        $lines.Add(('| {0} | {1} | {2} | {3} | {4} | ``images/{5}.png`` |' -f
            $row.Lang, $row.Engine, $row.Exit, ($row.Text -replace '\|', '\|'), $row.Conf, $row.Png))
    }
    $lines.Add('')
    $lines.Add('## Dark background (light text on dark), read raw versus normalised')
    $lines.Add('')
    $lines.Add('The picker overlay draws white text on a dark band, so it is a dark-background sample whose')
    $lines.Add('wording is known. `--normalize` is off by default now; this is what it buys.')
    $lines.Add('')
    $lines.Add('| mode | exit | read back | conf | screenshot |')
    $lines.Add('| --- | --- | --- | --- | --- |')
    foreach ($row in $darkRows) {
        $lines.Add(('| {0} | {1} | {2} | {3} | ``images/{4}.png`` |' -f
            $row.Mode, $row.Exit, ($row.Text -replace '\|', '\|'), $row.Conf, $row.Png))
    }
    $lines.Add('')
    $lines.Add('## Region picker')
    $lines.Add('')
    $lines.Add("- ``region pick --rect`` in client space: exit $($pickClient.Exit), space $($pickClient.Json.space), region $($pickClient.Json.region -join ',')")
    $lines.Add("- ``region pick --rect`` in window space (title bar): exit $($pickWindow.Exit), space $($pickWindow.Json.space), region $($pickWindow.Json.region -join ',')")
    $lines.Add("- ``region pick --rect 0,0,0,5``: exit $($pickBad.Exit) (a zero-sized rectangle is a usage error)")
    $lines.Add("- dragged rectangle: exit $dragExit$(if ($dragJson) { ", space $($dragJson.space), region $($dragJson.region -join ',')" })")
    $overlayNote = if ($overlayPng) { 'images/' + $overlayPng + '.png' } else { 'not captured' }
    $lines.Add('- the overlay itself: ' + '`' + $overlayNote + '` (probe read exit ' + $overlayShot.Exit + ')')
    $lines.Add("- ESC cancels: exit $escExit")
    $lines.Add('')
    $lines.Add('## Coordinate spaces')
    $lines.Add('')
    $lines.Add("- client space image:  ``$clientHash``  (``images/$clientPng.png``)")
    $lines.Add("- window space image:  ``$windowHash``  (``images/$windowPng.png``)")
    $lines.Add('- identical: ' + $(if ($clientHash -ne '' -and $clientHash -eq $windowHash) { 'yes' } else { 'NO' }))
    $lines.Add('')
    $lines.Add('## Title bar read')
    $lines.Add('')
    $lines.Add("- exit $($titleRead.Exit), confidence $($titleRead.Json.confidence), text ``$(($titleRead.Json.lines | ForEach-Object { $_.text }) -join ' / ')`` (``images/$titlePng.png``)")
    $lines.Add('')
    $lines.Add('## Files')
    $lines.Add('')
    $lines.Add('- `commands.txt` - every command line with exit code, stdout and stderr')
    $lines.Add('- `json/` - the JSON of every call, and the `.err.txt` of every failure')
    $lines.Add('- `images/` - every screenshot, as PNG; the `-raw` files are the pixels as captured')
    Set-Content -LiteralPath (Join-Path $run 'summary.md') -Value ($lines -join "`n") -Encoding utf8
    Write-Host "证据已归档: $run"
}
finally {
    Get-Process KeyMouse.SmokeTarget -ErrorAction SilentlyContinue | Stop-Process -Force
}
