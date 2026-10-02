#requires -Version 7
<#
    发布前的验证入口：把四道关卡串起来跑一遍，最后给出明确结论。

        pwsh .\verify.ps1              # 全跑（桌面锁着会自动跳过冒烟并说明原因）
        pwsh .\verify.ps1 -SkipSmoke   # 只跑不需要桌面的部分

    四道关卡：
        构建       dotnet build KeyMouse.sln -c Release      （0 警告是底线）
        单元测试   tests\KeyMouse.Tests                       （不需要桌面）
        文档链接   tests\check-docs.ps1                       （不需要桌面）
        桌面冒烟   tests\smoke.ps1                            （需要解锁的交互式桌面）

    退出码 0 = 全部通过；非 0 = 有东西没过，输出里会点名是哪一个。
#>
[CmdletBinding()]
param([switch]$SkipSmoke)

$ErrorActionPreference = 'Continue'
$root = $PSScriptRoot
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
# 与其让人对着 10 条红色断言怀疑人生，不如在这里就说清楚。
function Test-DesktopLocked {
    try {
        if (-not ('KmVerify.Win' -as [type])) {
            Add-Type -Namespace KmVerify -Name Win -MemberDefinition @'
[DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
[DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, System.Text.StringBuilder s, int n);
'@
        }

        $handle = [KmVerify.Win]::GetForegroundWindow()
        $title = [System.Text.StringBuilder]::new(256)
        [void][KmVerify.Win]::GetWindowText($handle, $title, 256)

        return ($title.ToString() -match '锁屏|Lock Screen') -or
               (@(Get-Process LockApp -ErrorAction SilentlyContinue).Count -gt 0)
    }
    catch {
        return $false   # 判不出来就别拦着人跑
    }
}

Push-Location $root
try {
    $commit = (& git rev-parse --short HEAD 2>$null)
    $dirty = (& git status --porcelain 2>$null | Measure-Object).Count -gt 0
    Write-Host "验证对象：commit $commit$(if ($dirty) { '（工作区有未提交改动）' })" -ForegroundColor Yellow

    Step '构建' { & dotnet build (Join-Path $root 'KeyMouse.sln') -c Release --nologo | Select-Object -Last 3 }
    Step '单元测试' { & dotnet run -c Release --project (Join-Path $root 'tests\KeyMouse.Tests\KeyMouse.Tests.csproj') | Select-Object -Last 2 }
    Step '文档链接' { & pwsh -NoProfile -File (Join-Path $root 'tests\check-docs.ps1') }
    Step '打包 dist\KeyMouse.exe' { & pwsh -NoProfile -File (Join-Path $root 'build.ps1') | Select-Object -Last 2 }

    if ($SkipSmoke) {
        $skipped.Add('桌面冒烟（-SkipSmoke）')
    }
    elseif (Test-DesktopLocked) {
        $skipped.Add('桌面冒烟（桌面锁着——冒烟测试会以"抢不到前台"失败，那是环境不是产品）')
    }
    else {
        Step '桌面冒烟' { & pwsh -NoProfile -File (Join-Path $root 'tests\smoke.ps1') }
    }
}
finally {
    Pop-Location
}

Write-Host ''
Write-Host '================ 结论 ================' -ForegroundColor Cyan
$exe = Join-Path $root 'dist\KeyMouse.exe'
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
Write-Host '  .\dist\KeyMouse.exe --help                      # 中文帮助是否符合预期'
Write-Host '  .\dist\KeyMouse.exe window list                 # 表格列是否对齐、状态是否是中文'
Write-Host '  .\dist\KeyMouse.exe run samples\notepad-demo.txt --dry-run   # 示例能不能跑（不发送任何输入）'
Write-Host ''
Write-Host '全绿之后告诉作者，由他打 tag 发布（发布不由 push 触发）。'

exit ([int]($failed.Count -gt 0))
