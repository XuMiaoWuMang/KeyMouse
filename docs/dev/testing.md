# 测试

KeyMouse 的测试分三层，界限是**能不能碰桌面**：单元测试证明逻辑对，冒烟测试证明它作用到真实窗口上还对，取证把读屏这类"概率性"能力的测量数据归档下来给人看。
三层怎么跑、各覆盖什么、什么时候必须跑、以及写新测试要守的规矩，都在这一页。

## 三层一览

| 层 | 入口 | 需要什么 | 覆盖 | 落在哪 |
| --- | --- | --- | --- | --- |
| 单元测试 | `.\km t` | 只要 .NET | 解析、选择、流程编译与执行、协议、纯函数 | 不落盘（跑已编译的 exe） |
| 桌面冒烟 | `.\km s` | 解锁的交互式桌面 + 靶子 exe | 完整链路作用在真窗口上的行为 | `artifacts\tests\smoke-<时间戳>\` |
| 取证 | `pwsh tools\diagnostics\evidence.ps1` | 同上 + OCR 引擎 | 读屏（`probe`）的准确率与代价 | `artifacts\tests\evidence-<时间戳>\` |

**有产物的两层，每次运行都在 `artifacts\tests\` 下新建一个带时间戳的目录**，互不覆盖、可以并排比较，也不污染别处。这条约定见下文「写新测试的规矩」。

新编辑器外壳（`src\KeyMouse.EditorUI`）不在这三层里，它有自己的两套：前端 `vitest`（纯函数：步骤增删排序、撤销后选中项、缺必填项判断）与外壳 `cargo test`（路径解析、读写流程、错误码）。两边由 `tools\dev\editor-ui.ps1 test` 一起跑，`verify.ps1` 的「新外壳」一关会调它；界面本身靠截图核对，不做端到端点击模拟。

## 单元测试

一个普通控制台程序，**故意不用任何测试框架**：零依赖、离线可跑、`dotnet run` 就能执行，非零退出码即失败。入口是 `tests\KeyMouse.Tests\TestEntry.cs`（类名不叫 `Program`，是为了让命名空间里的 `Program` 仍然解析到 CLI 的 `Program`——runner 测试要直接调它）。

```pwsh
.\km t                    # 全部 16 个模块
.\km t parsing flow       # 只跑改到的模块，可写多个
.\km tl                   # 列出模块
.\km t -n flow            # 不重新编译，直接跑
.\km t bench              # 解析的吞吐基准，不参与关卡
```

也可以直接跑已编译的 exe：

```pwsh
dotnet run --project tests\KeyMouse.Tests -- --only loops
tests\KeyMouse.Tests\bin\Release\net10.0-windows\KeyMouse.Tests.exe --list
```

16 个模块与各自的覆盖：

| 模块 | 覆盖 |
| --- | --- |
| `parsing` | 命令行解析、脚本语法与循环 |
| `layout` | 安装布局：编辑器名字、查找顺序与理由 |
| `windows` | 窗口选择、候选偏好与资格判定 |
| `probe` | 读屏共识、引擎调用与预处理 |
| `locator` | 文字定位：框、并集与容错 |
| `region` | 选区坐标换算与描述 |
| `flow` | 流程编译、条件与前置条件 |
| `loops` | 循环、变量与展平 |
| `calls` | 子流程：内联、作用域与导出 |
| `runner` | 常驻 Runner 协议与作业控制 |
| `protocol` | 前后端通信契约：方法、事件、参数与终结规则 |
| `newlines` | 换行归一：CRLF / 裸 CR 一律折成 LF |
| `wheel` | 滚轮：竖向与横向是两个方向 |
| `stopkeys` | 录制的停止键：Ctrl+Alt+Q 与连按两次 ESC |
| `timeline` | 录制时间线：`atMs` 的写入与往返 |
| `nowindow` | 无窗口执行：默认不要求窗口在前台 |

断言只有一个极小的 Harness（`tests\KeyMouse.Tests\Harness.cs`）：`Check(说明, 条件, 细节)`、`Equal`、`Sequence`、`Throws`，末尾打印 `N passed, M failed`，有失败就返回退出码 1。每个模块包在 `Section` 里跑，某个模块抛异常会被记成一条失败并继续跑后面的模块，不会让整轮测试中断。

最近一次全跑：16 个模块 **677 passed, 0 failed**（0 警告）。

**什么时候必须跑**：动了参数解析、窗口选择、流程编译/执行、Runner 协议、录制格式中的任何一处，跑对应模块；发布前跑全部。单测不需要桌面，所以在 CI 里也照跑。

## 桌面冒烟

用 KeyMouse 自己的鼠标键盘去驱动一个**专门的靶子窗口**（`tests\KeyMouse.SmokeTarget`），验证"真的作用到桌面上了"：退出码、输入有没有落地、窗口有没有被误动、进程数对不对。

```pwsh
.\km s                              # 全部 12 个模块（km 会把 bin 里的 CLI 传进去）
.\km s -Only typing,features        # 只跑几个
pwsh tests\smoke\run.ps1 -List      # 列出模块
pwsh tests\smoke\run.ps1            # 默认用 artifacts\cli\KeyMouse.exe
pwsh tests\smoke\run.ps1 -Exe artifacts\cli-self\KeyMouse.exe   # 自包含那份，要先生成：.\km d -s
```

注意两个默认值不一样：`.\km s` 把 `src\KeyMouse.Cli\bin\Release\net10.0-windows\KeyMouse.exe`（刚编出来那份）传给 `run.ps1`，而直接跑 `run.ps1` 默认用 `artifacts\cli\KeyMouse.exe`（发布出去那份）。**六道关卡里的第 6 关用的是后者**——关卡验的是要发出去的产物，不是编译中间物。

12 个模块（按顺序跑，共享同一个靶子与桌面）：

> **每个模块自己负责把舞台摆好。** 模块之间共享一个靶子窗口，"上一个模块把窗口最小化了""光标不在文本框里""终端窗口盖在靶子上"都是会真实发生的事。写新模块时按三条自检：要键盘就先点一下正文（`mouse click left -wx 120 -wy 60`），要判层序就先 `window focus`，要看某一步的过程就加 `run … --report <文件>` 把每条命令的退出码记下来。
>
> 还有一个环境坑：**PowerShell 宿主默认不是 DPI 感知的**。150% 缩放的机器上它报的窗口坐标是虚拟化的（1707×960），而 KeyMouse 用物理像素（2560×1440），两边算出的"同一个点"差着一次缩放，`region pick --rect` 就会选到别的窗口。`common.ps1` 载入时先调 `SetProcessDPIAware()`；自己写脚本用 P/Invoke 取坐标时也要先做这一步。

| 模块 | 覆盖 |
| --- | --- |
| `entry` | 退出码矩阵（未知选择器 3、坏参数与未知键 2、解析不到的目标 3）、靶子窗口起来且能聚焦 |
| `typing` | 脚本打字往返（两行、回车换行、中文逐字比对）、窗口相对拖拽真的选中了文字、`--set` 变量插值 |
| `safety` | 重试只重复"没发出输入"的失败（报告里 `attempts=3`、`injectedEvents=0`）、`waitfor`/`waitgone` 与超时 3、禁用窗口 4、`--dry-run` 零输入、`vk:` 逃生口、stdin、`--keep-going` 与报告 |
| `features` | `window inspect` 的判定与退出码 3、窗口相对移动报出的屏幕点与 `mouse pos` 一致、脚本里只写一次选择器就继承目标 |
| `focus` | 最小化窗口：没同意就不还原（退出码 4），同意了才还原并真的发得出去 |
| `loops` | 文本脚本里的 `repeat`、变量，以及**整个脚本自始至终 1 个进程** |
| `region` | 读屏端到端：打字 → `probe` 读回 → 用读到的内容做断言；选区坐标换算；缺引擎是退出码 6 |
| `record` | 录制 → 回放 JSON 流程；坏流程被拒绝 |
| `serve` | 常驻 Runner：`serve` 起进程、CLI 走命名管道驱动它 |
| `act` | 从"读到文字"到"照着文字点"：`probe --find` / `click-text` / `when` |
| `flowloops` | 流程 JSON 里的 `foreach` 与变量，`read-text` 把屏幕内容读进变量再插值 |
| `calls` | 子流程 `call`：默认值、作用域、导出 |

`tests\smoke\lib\common.ps1` 提供模块能直接用的东西：`$Exe`（被测的命令行）、`$RunDir`（本次运行的目录）、`$target` / `$targetInScript`（靶子的选择器，两种引号形态）、`Check 说明 条件 [细节]`、`Wait-ForWindow 正则 [超时] [稳定]`。它还负责：开头做一次中文往返自检（程序输出是中文，控制台码页不对就只报一条清晰错误并退出 1）、保存并在结束时还原剪贴板、拉起靶子后用 `window focus` 重试到成功。

**每次运行都新建目录**：`artifacts\tests\smoke-<yyyyMMdd-HHmmss>\`，里面有 `run.log`（`Start-Transcript` 的完整输出）、`summary.md`（每个模块的状态 / 耗时 / 失败数）、`summary.json`，以及模块自己写的临时脚本、JSON、截图。本次运行的 `TEMP` / `TMP` 也被指到该目录下的 `tmp\`：probe 的中间位图不再散落在系统临时目录里，一次运行的东西全在一个文件夹；顺带让自动化在那种"子进程写不了 `%TEMP%`"的受限环境里也能跑。只保留最近 10 次 `smoke-*` 目录（攒着只会把盘吃满）；一次运行的证据就在那个目录里，失败时先看 `summary.md` 再去 `run.log` 找上下文。退出码是"有没有失败"（0 / 1）；缺 exe 或缺靶子时直接报错退出 1，模块名写错退出 2，某个模块抛异常只记一条失败并继续跑后面的模块。

**什么时候必须跑**：改了输入发送、窗口选择/聚焦/还原、录制与回放、区域选举、流程执行（循环 / 子流程 / 前置条件）这类"只有真窗口才能证明"的行为，或者发布前。锁屏时不用手动跳过——`verify.ps1` 只看**前台窗口**判断（`LockApp` / `LogonUI`，或标题匹配「锁屏 / Lock Screen」），命中就跳过第 6 关并打印原因；不能靠"`LockApp` 进程在不在"判断，解锁后它常常还留着。

**第二种跑不了：会话收不到注入的输入。** 冒烟的每一条断言都假设"打进去的字真的进了窗口"。嵌套/沙箱会话、远程桌面被挂在后台时，`SendInput` 会**报告成功、事件却被丢掉**，于是几十条断言一起变红，而根因只有一个。所以冒烟开跑前先用 [`check-input.ps1`](../tools/check-input.md) 问一句：往靶子里打一串固定字、再 `Ctrl+A`/`Ctrl+C` 读回剪贴板，读到的正好是那串字才算可用；读不到就以退出码 3 停下（`-SkipInputCheck` 可跳过这次自检）。`verify.ps1` 同样先做这个实验，返回 3 就把第 6 关记为"已跳过"并写明原因——**跳过不是通过**，结论区会分开列。

## 取证

有些东西不是"对/错"，而是"准不准、值不值这个价"：读屏在各种取样与预处理下的准确率、光标留在区域里的影响、两个坐标系是不是同一批像素、标题栏读不读得到。这类结论没有断言可写，需要的是**可复查的数据**，所以单列一层。

```pwsh
pwsh tools\diagnostics\evidence.ps1                        # 20 个样本 × 5 组配置，失败最多重试 3 次
pwsh tools\diagnostics\evidence.ps1 -Samples 5 -Attempts 1  # 快速看一眼
```

每次运行在 `artifacts\tests\evidence-<yyyyMMdd-HHmmss>\` 下归档：

| 文件 / 目录 | 内容 |
| --- | --- |
| `commands.txt` | 每条命令行 + 退出码 + stdout + stderr |
| `json\` | 每次调用的原始 JSON，以及失败时的 `.err.txt` |
| `images\` | 每张截图（BMP 转成 PNG 便于查看） |
| `summary.md` | 数字表格：每组配置的"首次读对 / 重试后读对 / 退出码 6 次数 / 平均置信度 / 平均耗时"，附文件索引 |

它测的东西固定成一套尺子：20 条真实语料（中英混排、路径、数字、符号），5 组配置（默认、默认+归一化、3× 最近邻、3× 双线性、3× 双线性+归一化），还有光标在区域内的影响、语言集对比、暗底选区浮层的 raw / normalized、`region pick` 的三条路径（`--rect` 客户区 / 窗口矩形、真的拖动、`ESC`）、两个坐标系同一批像素的 sha256、标题栏读取。

退出码 6 表示"没看清"（两次采集不一致，通常是抓到了重绘中间帧），它和 3/4/5 一样属于**确定没发出输入**，所以脚本会按调用方会采用的方式重试，并在 `summary.md` 里同时报首次与重试后的结果——"重试救回来几次"本身就是结论的一部分。

它只驱动自己的冒烟靶子，不碰用户窗口。归档**故意不进 git**：一次 20 样本约 10 MB 截图，仓库留给代码与文档（`.gitignore` 覆盖整个 `artifacts/`）。与冒烟不同，**取证目录不自动清理**，看一眼之后自己删。

另外 `pwsh tools\diagnostics\window-levels.ps1` 列可见窗口所属进程的权限级别（普通 / 已提权 / 读不到），排查"点了没反应"这类 UIPI 问题时用；它不产生归档。

**什么时候必须跑**：改 `probe` 的取像、预处理、共识判定、选区块选逻辑之后；或者有人报"读数不准"时。它慢且要 OCR 引擎，不是每次改代码都跑——但结论要写进 [design.md](design.md) 的对应小节，并把归档留在本机。

## 写新测试的规矩

1. **每次测试都在新目录里跑。** 有产物的层不要写到固定路径：冒烟与取证的落盘位置必须是本次运行的 `$RunDir` / 归档根目录（`artifacts\tests\smoke-<时间戳>`、`artifacts\tests\evidence-<时间戳>`），脚本、JSON、截图都放里面。不许写死 `tests\` 或仓库根——两次运行会互相覆盖，失败时也没法回溯是哪一次。单测不落盘，所以没有目录；但新用例一旦要写文件（临时脚本、样例流程），同样落运行目录，并在结束时清理。
2. **不碰用户程序。** 只操作测试自己启动的靶子（`tests\KeyMouse.SmokeTarget`）或测试自己开的记事本；不往用户正在用的窗口发输入、不改用户的文件。剪贴板动了要还原，测试拉起的进程要在 `finally` 里结束，别留后台常驻。
3. **断言要有证据。** `Check` 的第三个参数写实际值——`exit=$LASTEXITCODE`、`expected [..], got [..]`、命令与输出片段。失败的人不该为了知道"到底哪不对"再跑一遍。取证的每一张图都配它对应的 JSON 与命令行。
4. **失败要能复现。** 测试自己造前置条件（先打字、等窗口稳定、复位状态），不依赖上一次运行的残留；和时序有关的检查用 `Wait-ForWindow` 而不是睡一个猜出来的毫秒数；"必须没有发生"的行为（没发输入、没多出进程、没抢焦点）要显式断言，别指望它自然成立。
5. **断言退出码，再断言文本。** 退出码是契约（3/4/5/6 表示一个字节都没发），文本会受编码、语言、DPI 影响——需要比对中文时，拿测试自己刚写进去的字做预期值。
6. **新模块要登记。** 单元测试在 `tests\KeyMouse.Tests\TestEntry.cs` 的 `Sections` 里加一行（名字、一句话说明、`Run` 方法）；冒烟在 `tests\smoke\run.ps1` 的 `$modules` 里加一项，并同步模块头注释的清单。冒烟模块按顺序共享同一个靶子和桌面，所以新模块要么自己把状态摆回去，要么放在不依赖前态的位置。
7. **两层都断言才算锁住。** 只有单元测试证明逻辑、冒烟测试证明它作用到桌面上，这个行为才算被固定——"脚本在单进程内执行"就是这么钉的：如果有人改成"每行起一个进程"，单测里那个注入的 dispatcher 根本不会被调到。取舍的完整讨论见 [design.md 的「测试策略」](design.md#测试策略)。

这些规矩没有自动检查，靠第 2 关（单元测试）与第 6 关（冒烟）的通过情况间接守；`verify.ps1` 的六道关卡见 [development.md](development.md)。
