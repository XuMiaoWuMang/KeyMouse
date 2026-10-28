# 桌面冒烟测试

[tests\smoke\run.ps1](../../tests/smoke/run.ps1) 真的开一个靶子窗口，让 KeyMouse 往里打字、拖拽、点文字，再从剪贴板或 JSON 读回来对账。它验的是"在真桌面上还成立"，单元测试验不了这些。

## 用法

```pwsh
pwsh tests\smoke\run.ps1                        # 全部模块（发布前跑这个）
pwsh tests\smoke\run.ps1 -Only loops,flowloops  # 只跑改到的模块（平时用这个，快）
pwsh tests\smoke\run.ps1 -List                  # 看有哪些模块
pwsh tests\smoke\run.ps1 -Exe path\to\KeyMouse.exe
.\km s                                          # 日常入口：用 bin 里刚编译的 exe，其余参数原样转发
```

| 参数 | 默认值 | 说明 |
| --- | --- | --- |
| `-Exe` | `artifacts\cli\KeyMouse.exe` | 被测的命令行 |
| `-TargetExe` | `tests\KeyMouse.SmokeTarget\bin\Release\net10.0-windows\KeyMouse.SmokeTarget.exe` | 靶子窗口 |
| `-Only` | 全部模块 | 逗号分隔的模块名，按给定顺序跑 |
| `-List` | 关 | 只列模块与一句话说明，退出码 0 |

`km s` 传的 `-Exe` 是 `src\KeyMouse.Cli\bin\Release\net10.0-windows\KeyMouse.exe` —— 刚编译出来的那份，不是 `artifacts\cli` 里可能过期的单文件。

## 十二个模块

按列表顺序执行（`entry` 先把靶子立起来并自检，后面的模块都往同一个靶子里打字）：

| 模块 | 测什么 |
| --- | --- |
| `entry` | 退出码、宿主编码自检、启动冒烟靶子 |
| `typing` | 打字与拖拽的真实往返 |
| `safety` | 重试/等待/禁用窗口/dry-run/stdin 等安全语义 |
| `features` | window inspect、窗口相对移动、脚本继承目标 |
| `focus` | 聚焦与还原：最小化窗口的同意语义、抢前台的实测 |
| `loops` | 脚本循环与"一个进程跑到底" |
| `region` | 选区浮层：拖拽/单击/ESC 与坐标系回归 |
| `record` | 录制与回放（含 wait-text） |
| `serve` | 常驻 Runner：CLI 当客户端驱动它 |
| `act` | `--find` / click-text / when |
| `flowloops` | 流程里的循环与变量 |
| `calls` | 子流程：内联、vars 与 export |

## 每次运行留什么

```
artifacts\tests\smoke-20261029-143012\
    run.log        完整输出（Start-Transcript，含每个模块的耗时）
    summary.md     人读的结论：通过数、失败数、每模块耗时与失败数
    summary.json   机读的结论：started / exe / modules[{module,seconds,failures}] / passed / failed
    tmp\           临时文件：本次运行的 %TEMP%（probe 的中间位图等）也指到这里
    各类脚本       模块自己的 .txt / .json 都落在这里
```

每次开始跑时按目录名倒序清理，**只保留最近 10 次**（刚建的这一次算在内）：取证靠最近几次，攒着只会把盘吃满。运行目录名带时间戳，多次运行互不覆盖。

## 为什么需要解锁的桌面

冒烟注入的是真实的键鼠事件、真的去抢前台、真的弹选区浮层。锁屏时谁也抢不到前台，焦点断言必然失败 —— 那是环境问题，不是产品问题。所以 [`verify.ps1`](verify.md) 在跑它之前会先判一次锁屏，被判锁屏就直接跳过并说明理由。手动跑时锁屏会看到一片红色，别急着怀疑代码。

## 退出码

| 退出码 | 含义 |
| --- | --- |
| `0` | 全部断言通过（`-List` 也是 0） |
| `1` | 有断言失败；或 `-Exe` / `-TargetExe` 不存在；或 `-Only` 里写了不存在的模块名 |
| `2` | 源码里给"未知模块"留了这个码，但 `Write-Error` 在 `$ErrorActionPreference='Stop'` 下会先把脚本终止，实测走不到（以脚本为准） |

`Check` 记下的失败不阻断后续模块：前一个模块红了，后面的照样跑，最后由 `summary.json` 汇总失败数；但模块里**抛异常**会中断剩下没跑的模块（`finally` 仍会清理并写出 summary，只是 `modules` 少几条）。收尾时脚本总会杀掉 `KeyMouse.SmokeTarget` 与残留的 `KeyMouse` 进程，并把剪贴板还原成运行前的内容。

## 写一个新模块

1. 新建 `tests\smoke\modules\<name>.ps1`，并在 `run.ps1` 的 `$modules` 有序表里登记一行说明（不登记就跑不到，`-Only <name>` 会以 2 退出）。
2. 模块由 `run.ps1` 用 `.` 载入，**不要**自己 dot-source `lib\common.ps1`；公共变量与函数已经在了：

| 名字 | 是什么 |
| --- | --- |
| `$Exe` | 被测的 KeyMouse.exe（绝对路径） |
| `$RunDir` | 本轮运行的目录：临时脚本、截图、报告都放这里（单独调试模块时回退到系统临时目录） |
| `$target` | 直接调 CLI 时传给 `--title` 的两个参数 |
| `$targetInScript` | 在脚本行里带引号的 `--title`（标题里有空格） |
| `Check <说明> <bool> [细节]` | 记一条断言，失败打印细节并累加失败数 |
| `Wait-ForWindow <正则> [超时毫秒] [稳定毫秒]` | 轮询 `window list --all` 等窗口出现，默认 8000 / 250 毫秒 |

3. 当前工作目录已经是 `$RunDir`，模块里的相对路径自然落在本轮目录，不会污染仓库。
4. 等窗口用 `Wait-ForWindow`，不要用固定 `Start-Sleep`：覆盖层起来要多久取决于抢前台快不快，固定睡眠迟早会变成"偶尔早到"。

`lib\common.ps1` 在载入时还会做两件基建：先跑一次 `mouse move 1 1` 自检中文输出能否正确解码（对不上就以 1 退出，并打印码页），再启动靶子并用 `window focus` 等到它真的能被聚焦。
