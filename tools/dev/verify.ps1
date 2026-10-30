#requires -Version 7
<#
    发布前验证入口：把所有自动关卡串起来跑一遍，最后给出明确结论。

        pwsh tools\dev\verify.ps1              # 全跑（桌面锁着会自动跳过冒烟并说明原因）
        pwsh tools\dev\verify.ps1 -SkipSmoke   # 只跑不需要桌面的部分
        pwsh tools\dev\verify.ps1 -SkipPack    # 不打包（只想确认代码是好的）

    六道关卡，全部自动，任何一道不过就以非 0 退出：
        构建       dotnet build KeyMouse.sln -c Release      （0 警告是底线）
        单元测试   tests\KeyMouse.Tests                       （不需要桌面）
        新外壳     svelte-check + cargo test                    （需要 pnpm 与 cargo，缺了跳过）
        文档链接   tools\dev\check-docs.ps1                   （不需要桌面）
        版本一致   tools\dev\check-version.ps1                （唯一来源在 Directory.Build.props）
        打包       tools\build\build.ps1 → artifacts\cli      （单文件产物能不能起来）
        桌面冒烟   tests\smoke\run.ps1                        （需要解锁的交互式桌面）

    冒烟这一关有两种"环境不具备"的跳过条件，都会在结论里写明原因：
        锁屏                    （tools\dev\verify.ps1 自己判定）
        本会话收不到注入的输入  （tools\dev\check-input.ps1 判定；嵌套/沙箱会话常见）

    每次冒烟都在 artifacts\tests\ 下开一个新目录，互不覆盖。

    退出码 0 = 全部通过；非 0 = 有东西没过，输出里会点名是哪一个。
#>
[CmdletBinding()]
param(
    [switch]$SkipSmoke,
    [switch]$SkipPack
)

$ErrorActionPreference = 'Continue'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$failed = [System.Collections.Generic.List[string]]::new()
$skipped = [System.Collections.Generic.List[string]]::new()

function Step([string]$name, [scriptblock]$body) {
    Write-Host ''
    Write-Host "== $name ==" -ForegroundColor Cyan
    & $body
    if ($LASTEXITCODE -ne 0) {
        $failed.Add($name)
        Write-Host "   -> 没过（退出码 $LASTEXITCODE）" -ForegroundColor Red
    } else {
        Write-Host '   -> 通过' -ForegroundColor Green
    }
}

# 锁屏时谁也别想抢到前台，冒烟测试会以"焦点验证失败"收场——那是环境，不是产品。
# 与其让人对着十几条红色断言怀疑人生，不如在这里就说清楚。
#
# 判据只看**前台窗口**：锁屏时它要么属于 LockApp/LogonUI，要么标题就是锁屏界面。
# 不能用"LockApp 进程还在不在"来判断——解锁之后它常常一直挂着（实测：中午启动，
# 晚上还在，但一个窗口都没有），那样会把好好的桌面误判成锁屏、白跳过一整关。
function Get-LockScreenReason {
    try {
        if (-not ('KmVerify.Win' -as [type])) {
            Add-Type -Namespace KmVerify -Name Win -MemberDefinition @'
[DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
[DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, System.Text.StringBuilder s, int n);
[DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
'@
        }

        $handle = [KmVerify.Win]::GetForegroundWindow()
        $title = [System.Text.StringBuilder]::new(256)
        [void][KmVerify.Win]::GetWindowText($handle, $title, 256)

        $owner = 0
        [void][KmVerify.Win]::GetWindowThreadProcessId($handle, [ref]$owner)
        $process = (Get-Process -Id $owner -ErrorAction SilentlyContinue).ProcessName

        # 返回"为什么判定锁屏"，而不是一个光秃秃的 true：跳过关卡时必须让人能一眼看穿
        # 这个判断对不对。刚才正是缺了这行证据，一个把 LockApp 残留当成锁屏的错判，
        # 连续两次让冒烟测试被静悄悄跳过去——验证"看起来做完了"，其实根本没跑。
        if ($process -in 'LockApp', 'LogonUI') {
            return "前台窗口属于 $process：「$($title.ToString())」"
        }
        if ($title.ToString() -match '锁屏|Lock Screen') {
            return "前台窗口标题是锁屏界面：「$($title.ToString())」"
        }
        return $null
    }
    catch {
        return $null   # 判不出来就别拦着人跑
    }
}

# .NET SDK 的中文输出在被重定向时按 UTF-8 写，而中文 Windows 控制台默认按 GBK 解，
# 于是"0 个错误"会显示成"0 涓敊璇?"。看着难受是小事——真出了编译错误，那堆中文信息也会
# 是乱码，那就没法读了。所以把控制台临时切到 UTF-8，脚本结束时切回去。
#
# KeyMouse 自己不用这么改：它跟随控制台码页（cp936 就写 GBK，cp65001 就写 UTF-8），
# 控制台切到 UTF-8 之后它自然跟着写 UTF-8，两边照样对得上。
$originalCodePage = 0
try { $originalCodePage = [int](((chcp) -split ':')[-1]).Trim() } catch { }
try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch { }
if ($originalCodePage -ne 0 -and $originalCodePage -ne 65001) {
    Write-Host "控制台码页 $originalCodePage -> 65001（为了让 dotnet 的中文输出不乱码，结束会切回）" -ForegroundColor DarkGray
}

Push-Location $root
try {
    $commit = (& git rev-parse --short HEAD 2>$null)
    $dirty = (& git status --porcelain 2>$null | Measure-Object).Count -gt 0
    Write-Host "验证对象：commit $commit$(if ($dirty) { '（工作区有未提交改动）' })" -ForegroundColor Yellow

    Step '构建' { & dotnet build (Join-Path $root 'KeyMouse.sln') -c Release --nologo | Select-Object -Last 3 }
    Step '单元测试' { & dotnet run -c Release --project (Join-Path $root 'tests\KeyMouse.Tests\KeyMouse.Tests.csproj') | Select-Object -Last 2 }
    # 新外壳（src\KeyMouse.EditorUI）是独立工具链：有 cargo 与 pnpm 才跑，没有就说清楚为什么跳过。
    $shellApp = Join-Path $root 'src\KeyMouse.EditorUI'
    $hasCargo = [bool](Get-Command cargo -ErrorAction SilentlyContinue)
    $hasPnpm = [bool](Get-Command pnpm -ErrorAction SilentlyContinue)
    if (-not (Test-Path $shellApp)) {
        $skipped.Add('新外壳（仓库里没有 src\KeyMouse.EditorUI）')
    }
    elseif (-not $hasCargo -or -not $hasPnpm) {
        $skipped.Add("新外壳（缺 $(if (-not $hasCargo) { 'cargo' } else { 'pnpm' })）")
    }
    else {
        Step '新外壳：类型检查' { & pwsh -NoProfile -File (Join-Path $root 'tools\dev\editor-ui.ps1') check | Select-Object -Last 2 }
        Step '新外壳：Rust 单测' { & pwsh -NoProfile -File (Join-Path $root 'tools\dev\editor-ui.ps1') test | Select-Object -Last 2 }
    }

    Step '文档链接' { & pwsh -NoProfile -File (Join-Path $root 'tools\dev\check-docs.ps1') }
    Step '版本一致' { & pwsh -NoProfile -File (Join-Path $root 'tools\dev\check-version.ps1') }

    # 示例流程被"打开 → 顺手保存"改坏过两次（步骤被删、文字丢了），而它正好是
    # 用户第一眼看的东西。这里钉住它的形状：6 步、展开后 8 次执行。
    Step '示例流程没被改坏' {
        $sample = Join-Path $root 'samples\demo-flow.json'
        $out = & (Join-Path $root 'artifacts\cli\KeyMouse.exe') run $sample --dry-run 2>&1 | Out-String
        if ($out -notmatch '8 步') { throw "samples\demo-flow.json 展开后应当是 8 步，实际：$($out -split "`n" | Select-Object -First 2)" }
    }

    if ($SkipPack) {
        $skipped.Add('打包产物（-SkipPack）')
    }
    else {
        Step '打包 artifacts\cli' { & pwsh -NoProfile -File (Join-Path $root 'tools\build\build.ps1') | Select-Object -Last 2 }
    }

    $lockReason = Get-LockScreenReason
    if ($SkipSmoke) {
        $skipped.Add('桌面冒烟（-SkipSmoke）')
    }
    elseif ($lockReason) {
        $skipped.Add("桌面冒烟（判定为锁屏：$lockReason）")
    }
    else {
        # 锁屏之外还有第二种"跑不了"：会话收不到注入的输入（嵌套/沙箱会话、远程桌面挂在后台）。
        # 那种情况下冒烟会红一片而根因只有一个，所以先用一句话的实验问清楚，再决定跑不跑。
        $inputCheck = & pwsh -NoProfile -File (Join-Path $root 'tools\dev\check-input.ps1') -Exe (Join-Path $root 'artifacts\cli\KeyMouse.exe') 2>&1
        $inputExit = $LASTEXITCODE
        if ($inputExit -eq 3) {
            Write-Host ''
            $inputCheck | Select-Object -Last 6 | ForEach-Object { Write-Host "  $_" -ForegroundColor DarkGray }
            $skipped.Add('桌面冒烟（本会话收不到注入的输入，见 tools\dev\check-input.ps1）')
        }
        elseif ($inputExit -ne 0) {
            $failed.Add("输入自检（退出码 $inputExit）")
        }
        else {
            Step '桌面冒烟' { & pwsh -NoProfile -File (Join-Path $root 'tests\smoke\run.ps1') -SkipInputCheck }
        }
    }
}
finally {
    Pop-Location
    if ($originalCodePage -gt 0 -and $originalCodePage -ne 65001) {
        try { [Console]::OutputEncoding = [System.Text.Encoding]::GetEncoding($originalCodePage) } catch { }
    }
}

Write-Host ''
Write-Host '================ 结论 ================' -ForegroundColor Cyan
$exe = Join-Path $root 'artifacts\cli\KeyMouse.exe'
if (Test-Path $exe) { Write-Host "产物版本：$(& $exe --version)" }

if ($failed.Count -eq 0) {
    Write-Host '自动关卡：全部通过' -ForegroundColor Green
}
else {
    Write-Host "自动关卡：没过的有 -> $($failed -join '、')" -ForegroundColor Red
}
foreach ($item in $skipped) { Write-Host "已跳过：$item" -ForegroundColor Yellow }

Write-Host ''
Write-Host '手动确认（自动关卡查不出来的那部分，2 分钟）：' -ForegroundColor Cyan
Write-Host '  .\artifacts\cli\KeyMouse.exe --help               # 中文帮助是否符合预期'
Write-Host '  .\artifacts\cli\KeyMouse.exe window list          # 表格列是否对齐、状态是否是中文'
Write-Host '  .\artifacts\cli\KeyMouse.exe doctor               # 环境判定：开发树还是安装版'
Write-Host '  .\artifacts\cli\KeyMouse.exe run samples\notepad-demo.txt --dry-run   # 示例能不能跑（不发送任何输入）'
Write-Host '  .\km env                                          # 编辑器看到的 CLI 是哪一个'
Write-Host ''
Write-Host '全绿之后告诉作者，由他打 tag 发布（发布不由 push 触发）。'

exit ([int]($failed.Count -gt 0))
