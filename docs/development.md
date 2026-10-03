# 开发与发布流程

命令与选项见 [reference.md](reference.md)，设计取舍见 [design.md](design.md)。

## 四道关卡

| 关卡 | 命令 | 需要桌面 | CI 会跑 |
| --- | --- | --- | --- |
| 构建 | `dotnet build KeyMouse.sln -c Release` | ❌ | ✅ |
| 单元测试 | `dotnet run -c Release --project tests\KeyMouse.Tests` | ❌ | ✅ |
| 文档链接检查 | `pwsh tests/check-docs.ps1` | ❌ | ✅ |
| **桌面冒烟** | `pwsh tests\smoke.ps1` | ✅ | ❌（runner 没有交互式桌面） |
| 性能基准 | `dotnet run -c Release --project tests\KeyMouse.Tests -- bench` | 部分 | ❌（按需手动跑） |

前两项是"逻辑没坏"，第三项防文档腐烂，第四项是**唯一能证明"手没抖"的关卡**。

## 一条命令跑完全部关卡

```powershell
pwsh .\verify.ps1              # 构建 → 单元测试 → 文档链接 → 打包 → 桌面冒烟
pwsh .\verify.ps1 -SkipSmoke   # 只跑不需要桌面的部分
```

`verify.ps1` 会自动识别**桌面锁着**的情况并跳过冒烟测试（锁屏时谁都抢不到前台，
跑了只会得到一堆"焦点验证失败"，那是环境不是产品）。退出码 0 = 全部通过。

自动关卡查不出的部分（中文观感、表格对齐、示例能不能照着用）脚本末尾会列出三条手动命令。

## 改一处代码

1. 改代码；
2. `dotnet build KeyMouse.sln -c Release` —— **0 警告是底线**；
3. `dotnet run -c Release --project tests\KeyMouse.Tests` —— 纯逻辑，秒级；
4. 只要碰到 **窗口选择 / 闸门 / 注入 / 脚本执行** 任一条路径 → 跑 `pwsh tests\smoke.ps1`；
5. 提交：一个提交做一件事，消息里写清**为什么**（"改了什么"看 diff 就知道）；
6. `git push`。

`tests\smoke.ps1` 会启动 `tests\KeyMouse.SmokeTarget`（一个整块客户区都是文本框的小窗口），
把它当作靶子跑完 45 项断言，最后只结束**它自己启动的那个进程**——**不会碰你开着的任何程序**。
它会临时占用剪贴板（结束还原），失败时返回非零退出码。

> **控制台编码**：程序按**控制台自己的码页**输出（cp936 就写 GBK，65001 就写 UTF-8），
> 因为 PowerShell 正是用 `[Console]::OutputEncoding`（跟随控制台码页）解码原生命令输出的。
> 冒烟脚本开头会先做一次中文往返检查：如果这个宿主把两者设得不一致，它会**只报一条清晰错误**
> 并退出，而不是让后面十几条中文断言莫名其妙地失败。

## 发布流程

**发布不由 push 触发，由 tag 触发，而且必须等人工验证。**

1. 候选改动已经在 `main` 上、CI 绿；
2. **由人验证**：`pwsh .\verify.ps1` 全绿（需要解锁的桌面），再手动试一下这次改动的功能；
3. 验证通过后再打 tag：

   ```powershell
   git tag -a v1.6.0 -m "KeyMouse v1.6.0 - ..."
   git push origin v1.6.0
   ```

4. tag 触发 `.github/workflows/release.yml`：先跑单元测试，再构建两个产物，附加到 GitHub Release；
5. 收尾：确认 Release 页面两个文件都在，**下载其中一个实跑一次 `--version`**。

> 为什么不让 push 直接发布：这个工具最糟的失败模式是"输入发到了错误的窗口"，
> 而这类问题只有真桌面上跑一遍才看得出来。CI 能证明逻辑没坏，不能证明手没抖。

## CI 做什么

`.github/workflows/ci.yml`（每次 push / PR）：构建解决方案 → 单元测试 → 文档链接检查 →
做一次发布构建（保证发布命令本身没坏）。

`.github/workflows/release.yml`（推 `v*` tag）：单元测试（不过就不发）→ 构建
`KeyMouse-fx-x64.exe`（框架依赖，约 240 KB）与 `KeyMouse-x64.exe`（自包含，约 36 MB）→
附加到同名 Release 并生成 release notes。

## 加新检查时放哪里

**只有在这两层都断言过的行为，才算被锁住**：单元测试证明逻辑，冒烟测试证明它真的作用到了桌面上。
"脚本在单进程内执行"就是这么钉住的——单元测试注入 dispatcher 数调用次数，冒烟测试采样进程数。

| 检查的性质 | 放哪 |
| --- | --- |
| 纯逻辑（解析、匹配、筛选、状态串） | `tests/KeyMouse.Tests` —— 零依赖控制台程序，**不要引测试框架** |
| 需要真窗口 / 真注入 | `tests/smoke.ps1` 里新加一节，用 `Check` 断言并给出失败细节；靶子是 `tests/KeyMouse.SmokeTarget`，**不要借用用户自己的程序** |
| 需要看数字 | `ParseBench`（`-- bench`），它已经会同时报"解析"和"执行"两边的成本 |
| 文档一致性 | `tests/check-docs.ps1` |

单元测试故意不用 xunit/NUnit：一个普通控制台程序，零依赖、离线可跑、`dotnet run` 就是全部用法。

## 目录（一个产品，两个入口，三层）

| 路径 | 是什么 |
| --- | --- |
| `src\KeyMouse.Core` | **能力层**：流程解释器、SendInput、全局钩子/录制、OCR/读屏、取区域、窗口闸门、流程 JSON。不认识 IPC，也不认识 UI |
| `src\KeyMouse.Runner` | **常驻引擎**：命名管道协议、作业注册表（排队/暂停/取消）、事件流、`Execution.Control` 接缝的实现 |
| `src\KeyMouse.Cli` | **命令行入口**（产物仍是 `dist\KeyMouse.exe`）：`serve` 起常驻服务，`runner ...` 当客户端，其余命令走能力层 |
| `editor\KeyMouse.FlowEditor` | **图形入口**（WinUI 3）：引用 Core 与 Runner，不复制 schema；取区域与回放都通过常驻 Runner |
| `tests\KeyMouse.Tests` | 单测（不需要桌面）：解析、窗口、probe、选区、流程、Runner 协议 |
| `tests\smoke.ps1` | 桌面冒烟：真输入真窗口，含"常驻 Runner 被 CLI 驱动"那一段 |
| `normify-keymouse\` | 结构数据（模块树 + 渲染图），`normify.html` 可下钻 |

## 平时怎么测：分模块，别每次都拉上全部

测试和代码一样按模块分。**改哪个模块就跑哪个模块；功能确定没问题了，再一次总测试。**

```powershell
# 单元测试（不需要桌面）：8 个模块，各一个文件
dotnet run -c Release --project tests\KeyMouse.Tests -- --list            # 看有哪些（现有 9 个）
dotnet run -c Release --project tests\KeyMouse.Tests -- --only loops     # 只跑该模块
dotnet run -c Release --project tests\KeyMouse.Tests                     # 全部

# 桌面冒烟：10 个模块，各一个文件（tests\smoke\）
pwsh tests\smoke.ps1 -List                                               # 看有哪些（现有 12 个）
pwsh tests\smoke.ps1 -Only typing,flowloops                              # 只跑相关的
pwsh tests\smoke.ps1                                                     # 全部

# 总测试（发布前、或一个功能定稿后）
pwsh .\verify.ps1
```

| 单元模块 | 覆盖 | 文件 |
| --- | --- | --- |
| `parsing` | 命令行解析、脚本语法与循环 | `tests\KeyMouse.Tests\ParsingTests.cs` |
| `windows` | 窗口选择、候选偏好、资格判定 | `WindowTests.cs` |
| `probe` | 读屏共识、引擎调用、预处理 | `ProbeTests.cs` |
| `locator` | 文字定位：框、并集、容错 | `LocatorTests.cs` |
| `region` | 选区坐标换算与描述 | `RegionTests.cs` |
| `flow` | 流程编译、条件与前置条件 | `FlowTests.cs` |
| `loops` | 循环、变量与展平 | `LoopTests.cs` |
| `calls` | 子流程：内联、作用域与导出 | `CallTests.cs` |
| `runner` | 常驻 Runner 协议与作业控制 | `RunnerTests.cs` |

| 冒烟模块 | 覆盖 | 文件 |
| --- | --- | --- |
| `entry` | 退出码矩阵、编码自检、靶子在位 | `tests\smoke\entry.ps1` |
| `typing` | 打字往返、拖拽、变量替换 | `typing.ps1` |
| `safety` | 重试/等待/禁用窗口/dry-run/stdin | `safety.ps1` |
| `features` | window inspect、相对移动、目标继承 | `features.ps1` |
| `focus` | 聚焦与还原：同意语义、抢前台 | `focus.ps1` |
| `loops` | 文本脚本的循环与"一个进程跑到底" | `loops.ps1` |
| `region` | 选区浮层与坐标系回归 | `region.ps1` |
| `record` | 录制与回放（含 wait-text） | `record.ps1` |
| `serve` | 常驻 Runner 被 CLI 驱动 | `serve.ps1` |
| `act` | `--find` / `click-text` / `when` | `act.ps1` |
| `flowloops` | 流程格式的循环与变量 | `flowloops.ps1` |
| `calls` | 子流程：vars、export、作用域不外泄 | `calls.ps1` |

两条规矩：

- **模块之间不互相牵连**：冒烟的公共部分（`tests\smoke\common.ps1`：`Check`、`$Exe`/`$target`、编码自检、
  剪贴板保存、**启动靶子**）在运行器里先载入，所以任何模块都能单独跑；基建不下放给某个模块。
- **总测试只有两个**：`pwsh verify.ps1`（构建 + 单元 + 文档链接 + 冒烟 + 发布检查）。
  平时别跑它——跑它意味着"这个功能我认为没问题了"。
## 实测留档（tests/evidence，只在本地）

跑一次测量、把**所有**证据落盘，供人回看：

```powershell
pwsh tests/evidence.ps1                      # 20 个样本 × 5 组配置，跑到读对为止（最多 3 次）
pwsh tests/evidence.ps1 -Samples 4 -Attempts 1
```

产物在 `tests/evidence/<时间戳>/`：`commands.txt`（每条命令 + 退出码 + stdout/stderr）、
`json/`（每次调用的 JSON 与失败时的 `.err.txt`）、`images/`（每张截图，`-raw` 是抓到的原始像素）、
`summary.md`（数字表 + 文件索引）。它只驱动自己的冒烟靶子，不碰用户的窗口。

**归档不入库**（`.gitignore` 忽略 `tests/evidence/`）：一次 20 样本的扫描就是 10 MB 级，
仓库留给代码与文档；留档的意义是"我当时看到的就是这些"，跑完在本地看即可。

写这类测量时有两条**踩过的坑**（不遵守就会量出假结论）：

1. **别让文字光标留在读取区域里**：`probe` 的比较是**逐字节**的（它不折叠空白、不剔光标——读到的文字原样回报，
   两次不一致就是退出码 6），而光标会闪；更糟的是光标进画面能让整行读崩
   （`你好，世界` 84.7 → 加光标 `Re,Hh` 49.8）。做法是打完字后 `key press enter -n 3` 把它挪到区域外。
2. **区域高度要盖住整行**：`0,0,400,20` 会切掉字底、同一行读成乱码，`0,0,400,32` 正常。
5. **"抢前台"是 Windows 上最容易假成功的事**：`SetForegroundWindow` 在调用者不持有前台时会被前台锁拒绝
   （实测：三次尝试全败，退出码 5，前台是别的进程）。要么先 `AttachThreadInput`，要么就别声称聚焦成功——
   KeyMouse 的做法是**回读真实前台窗口**，并把当前前台是谁写进错误信息。
4. **读屏有字号下限**：冒烟靶子的小字号下，复杂汉字读不准——「子流程:甲」读成 `FRE: F`，置信度 44.6，
   把横带从 24px 加到 64px **一点没变**（24px 时 9.8，32px 时 60.0，40/48/64px 都是 44.6）。所以
   读屏能力用**够大的字**单独验，UI 用例里用 ASCII 数据——否则测的是字体不是功能。
3. **别在选区上读**：选中反色会把同一行的置信度从 92.0 打到 65.0、多读字符甚至读空（实测）。
   做法是先点一下区域外：既取消选区，也把光标挪走。

## 架构结构数据（normify）

`normify-keymouse/` 是这份代码库的模块树，**与代码一起版本化**：

| 文件 | 是什么 |
| --- | --- |
| `normify.html` | 单文件交互式架构图：点框下钻、悬停看介绍、`?lang=en` 切英文、`#module=<id>` / `#api=<key>` / `#view=outline` 深链直达 |
| `tree.json` | 编译产物（含各层渲染数据） |
| `outline.md` | 广度优先的派生索引，给 AI 导航用 |
| `api-index.json` | 255 个 API 的索引 |
| `receipt.json` | 回执：统计、SHA-256 冻结、warning 计数 |
| `modules/` | 169 个模块文件（frontmatter = 机器读，正文 = 人读） |
| `renders/` | 每一层的渲染数据（顺序 / 分组 / 模式 / 阅读导语） |

粒度是**单一功能单元**：`NativeInput.TypeText`、`WindowEligibility.Check`、`ScriptRunner.ParseRepeat`、
`smoke.loops` 都各占一格。194 条箭头锚定到了具体 API 行，所以图上读到的是
`mouse click → rpc:NativeInput.Click`，而不是两个匿名框之间一条线。

改动代码后同步（伴随开发流程）：

1. `normify_sync`（`repoRoot` = 本仓库）→ 脏子树 / 新增文件建议 / 失效 source / API 增删与破坏性变更清单；
2. 按清单**局部**重建受影响模块（`normify_module_upsert` / `normify_module_patch` / `normify_module_move`），
   并在子级变化后同轮更新那一层的渲染数据；
3. `normify_validate` 必须 **0 error**（warning 可以留，但要能解释）；
4. `normify_build` → `normify_render`。

**不变量**：结构数据是**只读代码的派生物**——它不参与编译、不改变程序行为，
维护它的工具只写 `normify-keymouse/`，从不修改源码。文档说的和代码不一致时，
以代码为准，然后**两边一起改**。

## 不变量（改代码时别破坏）

1. **脚本在单进程内执行**：`ScriptRunner` 通过委托调用 `Program.Main` 的派发逻辑，
   **执行器**不允许 `Process.Start` / `ProcessStartInfo`——一行一条命令就是一个委托调用。
   进程启动开销、目标继承、循环变量全都建立在这条上。
   唯一的例外是**感知层调用显式配置的外部 OCR 引擎**（`probe --engine`）：那不是"每条命令起一个进程"，
   而是一次读取里调用一次外部工具，且引擎身份会写进输出。改这条要同时改本节与 design.md。
2. **失败即关闭**：退出码 `3` / `4` / `5` / `6` 必须能证明"一个字节都没发"，
   所以只有它们允许重试；`1` 可能已经发了一半，永远不重试。
3. **报错在动手之前**：解析、选择器、循环结构、变量名——所有能静态检查的东西
   都在第一条命令执行前检查完，不能出现"跑到一半才发现脚本写错"。
4. **不碰用户的东西**：冒烟测试只用自己启动的靶子窗口；不改用户的程序、不杀掉用户打开的窗口。

## 版本号

`KeyMouse.csproj` 的 `<Version>` 和 `Program.Version` **必须一致**（内置帮助从后者取版本）。
未发布前的多次改动共用一个版本号；发布之后才 +1。
