# verify.ps1 —— 发布前验证

[verify.ps1](../../tools/dev/verify.ps1) 把所有自动关卡串起来跑一遍，最后给一个明确结论：过了就过了，没过就点名是哪一关。对外交付前跑它，`km v` 是同一件事的日常写法。

## 用法

```pwsh
pwsh tools\dev\verify.ps1              # 全跑（桌面锁着会自动跳过冒烟并说明原因）
pwsh tools\dev\verify.ps1 -SkipSmoke   # 只跑不需要桌面的部分
pwsh tools\dev\verify.ps1 -SkipPack    # 不打包（只想确认代码是好的）
.\km v                                 # 等价的日常入口
```

| 参数 | 说明 |
| --- | --- |
| `-SkipSmoke` | 跳过桌面冒烟，结论里记为"已跳过" |
| `-SkipPack` | 跳过打包关卡，结论里记为"已跳过" |

## 六道关卡

| 关卡 | 实际执行 | 需要桌面？ |
| --- | --- | --- |
| 构建 | `dotnet build KeyMouse.sln -c Release --nologo`（只显示最后 3 行） | 否 |
| 单元测试 | `dotnet run -c Release --project tests\KeyMouse.Tests\KeyMouse.Tests.csproj` | 否 |
| 文档链接 | `tools\dev\check-docs.ps1` | 否 |
| 新外壳 | tools\dev\editor-ui.ps1 的 check 与 test：界面类型检查 + 外壳 Rust 单测 | 有 pnpm 与 cargo 才跑；缺哪个就在结论区写明跳过 |
| 版本一致 | `tools\dev\check-version.ps1` | 否 |
| 打包 | `tools\build\build.ps1` → `artifacts\cli\KeyMouse.exe` | 否 |
| 桌面冒烟 | `tests\smoke\run.ps1` | 是（且要解锁） |

冒烟这一关有两个前提，缺任何一个都会**跳过**（不是失败），并在结论里写明原因：

| 前提 | 判定方式 | 结论里长什么样 |
| --- | --- | --- |
| 桌面没锁屏 | `Get-LockScreenReason` 看前台窗口 | 已跳过：桌面冒烟（判定为锁屏：…） |
| 会话收得到注入的输入 | 先跑 [`check-input.ps1`](check-input.md)，返回 3 就跳过 | 已跳过：桌面冒烟（本会话收不到注入的输入…） |

第二种是嵌套/沙箱会话、远程桌面被挂在后台时的典型情况：`SendInput` 报告成功、事件却被丢掉，跑下去只会得到几十条假红。跳过时会把自检的实测输出（期望串与读到的串）附在结论上方，方便判断这个结论对不对。

关卡的组合方式是"能连续跑完"：`Step` 收集失败项后继续下一关，所以一次运行能看到全部问题；单独复跑某一关更快。

## 锁屏判定：为什么看前台窗口，而不是看 LockApp 进程

锁屏时谁也抢不到前台，冒烟会以"焦点验证失败"收场 —— 那是环境问题，不是产品问题。与其让人对着十几条红色断言怀疑人生，`Get-LockScreenReason` 在跑冒烟前先判一次：

- 取前台窗口（`GetForegroundWindow`）的所属进程名与标题；
- 进程名是 `LockApp` 或 `LogonUI` → 判定锁屏，返回原因；
- 标题匹配 `锁屏|Lock Screen` → 判定锁屏，返回原因；
- 判定过程抛异常 → 返回 `$null`（判不出来就别拦着人跑）。

**不能用"LockApp 进程还在不在"来判断**：解锁之后它常常一直挂着（实测：中午启动，晚上还在，一个窗口都没有），那样会把好好的桌面误判成锁屏、白跳过一整关 —— 之前就因为缺"为什么判定锁屏"这句证据，连续两次让冒烟被静悄悄跳过。所以跳过关卡时打印的是完整理由，例如 `桌面冒烟（判定为锁屏：前台窗口属于 LockApp：「锁屏界面」）`。

## 控制台码页

.NET SDK 的中文输出在被重定向时按 UTF-8 写，中文 Windows 控制台默认按 GBK 解，"0 个错误"会显示成"0 涓敊璇?"。脚本先把控制台切到 UTF-8（`[Console]::OutputEncoding`），结束时切回原码页：

```
控制台码页 936 -> 65001（为了让 dotnet 的中文输出不乱码，结束会切回）
```

KeyMouse 自己不用改：它跟随控制台码页（cp936 写 GBK，cp65001 写 UTF-8），控制台切到 UTF-8 后两边照样对得上。码页本来就是 65001 时这场切换静默进行。

## 输出与退出码

开头打印验证对象：

```
验证对象：commit 3f1c0a2（工作区有未提交改动）
```

结尾打印结论段：产物版本（`artifacts\cli\KeyMouse.exe --version`，存在才打印）、`自动关卡：全部通过` 或 `自动关卡：没过的有 -> …`、以及每条"已跳过：…"。

退出码只有两个值：`0` = 全部通过；`1` = 至少一关没过。跳过的关卡不算失败。

## 手动确认清单

自动关卡查不出来的那部分，脚本在结论后直接列出来（约 2 分钟）：

```pwsh
.\artifacts\cli\KeyMouse.exe --help               # 中文帮助是否符合预期
.\artifacts\cli\KeyMouse.exe window list          # 表格列是否对齐、状态是否是中文
.\artifacts\cli\KeyMouse.exe doctor               # 环境判定：开发树还是安装版
.\artifacts\cli\KeyMouse.exe run samples\notepad-demo.txt --dry-run   # 示例能不能跑（不发送任何输入）
.\km env                                          # 编辑器看到的 CLI 是哪一个
```

全绿之后告诉作者，由他打 tag 发布 —— 发布不由 push 触发。

## 注意

- "版本一致"关卡在"打包"之前跑，因此它检查的是**上一次**的 `artifacts\cli\KeyMouse.exe`；产物还不存在时它会打印"跳过产物版本检查"。产物新鲜度最终看结论段那次 `--version`。
- 单元测试关卡走的是 `dotnet run`（会顺带编译），日常快速循环用 `km t` 更快。
- 冒烟是数分钟级，其余五关都在秒级；平时改代码不该跑全套。
