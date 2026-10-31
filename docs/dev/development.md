# 开发与发布

这份文档给改 KeyMouse 代码的人：环境、`km` 的每个动词、二进制落在哪、编辑器怎么迭代、测试与关卡怎么过、版本与发布怎么走。
命令与选项本身见 [命令参考](../reference/cli.md)，设计取舍见 [design.md](design.md)，测试细节见 [testing.md](testing.md)。

## 环境

| 需要 | 说明 |
| --- | --- |
| Windows 10 / 11（x64） | 引擎直接调 Win32 / UIA / WinRT，编辑器是 WinUI 3 桌面应用 |
| .NET SDK 10 | 所有项目都是 `net10.0-windows*`；仓库里没有 `global.json`，用机器上装好的那个 SDK |
| PowerShell 7（`pwsh`） | `km.ps1`、`verify.ps1`、`check-*.ps1`、`build.ps1` 与 `tests\smoke\` 下的每个脚本都声明了 `#requires -Version 7`；Windows PowerShell 5.1 跑不起来 |

构建本身不需要管理员权限。桌面冒烟需要一个已解锁的交互式桌面——锁屏时会自动跳过（见「六道关卡与 CI」）。

```pwsh
dotnet --version      # 10.x
pwsh -v               # 7.x
```

## 入口：`km`

仓库根只有两个文件参与日常操作：`km.cmd`（给从 `cmd` / 资源管理器启动的人）和它转调的 `tools\dev\km.ps1`。两种写法等价：

```pwsh
.\km b                 # 推荐：从仓库根
pwsh tools\dev\km.ps1 b
```

不带动词会把动词表打印出来。每个动词只做一件事，默认只做该做的那件（逐条说明另见 [tools/km.md](../tools/km.md)）：

| 动词 | 做什么 | 大致耗时 |
| --- | --- | --- |
| `b` / `build` | 只编 CLI（连带 Core 与 Runner），不碰编辑器 | ~1 s |
| `ba` / `build-all` | 编整个解决方案（含 WinUI 编辑器与安装程序） | ~18 s |
| `e` / `editor` | 编编辑器，并把 CLI 发布到 `artifacts\cli`（编辑器连的就是它） | 编辑器 XAML + ~5 s |
| `e -NoCli` | 只编编辑器，不碰 `artifacts` | 同上 |
| `e -NoElevate` | 普通权限构建编辑器（换用 `app.unelevated.manifest`，不弹 UAC） | 同上 |
| `u` / `ui` | 起新编辑器外壳（Tauri + Svelte）的开发模式，等价于 `editor-ui.ps1 dev` | 首次编 Rust 数分钟 |
| `ub` / `ui-build` | 打新外壳的发布版（`pnpm build` + `cargo build --release`） | 分钟级 |
| `ut` / `ui-test` | 新外壳的两层测试（vitest + cargo test） | 十几秒 |
| `t` / `test` | 单元测试，全部模块（不需要桌面） | ~1 s |
| `t parsing` | 只跑指定模块，可写多个：`t parsing flow` | ~1 s |
| `tl` / `test-list` | 列出 16 个单元测试模块 | 瞬时 |
| `t -n flow` | 不重新编译，直接跑 | ~0.2 s |
| `s` / `smoke` | 桌面冒烟（需要解锁的桌面） | 数分钟 |
| `v` / `verify` | 六道关卡（构建 / 单测 / 文档 / 版本 / 打包 / 冒烟） | 取决于是否冒烟 |
| `d` / `dist` | 打包单文件到 `artifacts\cli\KeyMouse.exe` | ~5 s |
| `d -s` | 打包自包含单文件（体积大，目标机不需要 .NET 运行时） | ~10 s |
| `r key type hi` | 运行刚编出来的 KeyMouse，参数原样转发 | — |
| `r -n --help` | 不重新编译，直接运行 | — |
| `w` | 监视 `src\` 与 `tests\`：每次改动跑一遍「编 CLI + 编测试 + 全部单测」 | 每轮 ~3 s |
| `w -once` | 只跟一次（用来确认监视本身是好的） | ~3 s |
| `env` / `environment` | 打印 `KeyMouse doctor`：当前是开发树还是安装版、两边的命令行在哪 | ~1 s |
| `docs` | 文档链接检查（`tools\dev\check-docs.ps1`） | 瞬时 |
| `c` / `clean` | 删掉 `src\`、`tests\` 下的 `bin`/`obj` 与整个 `artifacts\` | 瞬时 |

`.vscode\tasks.json` 把这批动词挂成了任务（默认构建任务是 `km: build CLI`，`km: test (pick module)` 会弹出模块选择），任务面板里跑的就是同一条命令。

## 构建与产物流向

```text
src\KeyMouse.Cli\KeyMouse.Cli.csproj
  └─ dotnet build   (.\km b)      → src\KeyMouse.Cli\bin\Release\net10.0-windows\KeyMouse.exe
  └─ dotnet publish (.\km d)      → artifacts\cli\KeyMouse.exe          （框架依赖单文件）
  └─ dotnet publish -s (.\km d -s)→ artifacts\cli-self\KeyMouse.exe     （自包含单文件 + 压缩）

src\KeyMouse.FlowEditor\KeyMouse.FlowEditor.csproj
  └─ dotnet build   (.\km e / ba) → src\KeyMouse.FlowEditor\bin\Release\net10.0-windows10.0.26100.0\win-x64\
```

| 产物 | 谁生成 | 谁在用 |
| --- | --- | --- |
| `src\KeyMouse.Cli\bin\Release\net10.0-windows\KeyMouse.exe` | `.\km b` | `km r`、`km s`（把这份传给冒烟） |
| `artifacts\cli\KeyMouse.exe` | `.\km d`、`.\km e`、`pwsh tools\build\build.ps1` | 开发树里的编辑器、`km env`、`tests\smoke\run.ps1` 的默认值 |
| `artifacts\cli-self\KeyMouse.exe` | `.\km d -s`、`pwsh tools\build\build.ps1 -SelfContained` | 发布用的自包含命令行 |
| 编辑器 exe | `.\km e`、`.\km ba` | 开发树里直接双击运行 |
| `artifacts\release\` | `pwsh tools\build\pack-release.ps1` | 发布页三件套（见「版本与发布」） |

两条容易踩的事实：

- 编辑器输出路径里有 `net10.0-windows10.0.26100.0\win-x64`，**没有** `bin\x64\Release` 这种形态——项目的 `<AppendPlatformToOutputPath>false</AppendPlatformToOutputPath>` 就是为此加的（否则 `km ba` 与 `km e` 会写出两个不同的 exe，白查过一次）。
- `artifacts\` 整个不进 git，`.\km c` 会把 `bin`/`obj`/`artifacts` 一起删掉。

为什么 `km b` 默认只编 CLI：那 ~18 秒的绝大部分花在 WinUI 编辑器的 XAML 编译上，只改 Core 时 CLI 重建只要 1.7 秒。编辑器不参与，就不该付这笔钱；要它参与就用 `km ba` 或 `km e`。

### 开发树还是安装版

判定只在一个地方：`src\KeyMouse.Core\Product\ProductLayout.cs`。它从 exe 往上找同时有 `KeyMouse.sln` 与 `Directory.Build.props` 的目录来认「开发树」，否则认「安装版」，并据此决定命令行在 `<仓库根>\artifacts\cli\KeyMouse.exe` 还是 `<安装目录>\cli\KeyMouse.exe`。`KeyMouse doctor`（也就是 `.\km env`）会把每个候选路径和判定结果按顺序列出来。

开发树里没有 `cli` 文件夹是正常的：那是安装版的形状。候选顺序与症状表见 [environments.md](../guide/environments.md)。

## 编辑器开发流程

1. `.\km e` —— 编编辑器（WinUI 3），随后把 CLI 重新发布到 `artifacts\cli`。
2. 运行 `src\KeyMouse.FlowEditor\bin\Release\net10.0-windows10.0.26100.0\win-x64\KeyMouse.FlowEditor.exe`，或从命令行 `KeyMouse flow edit 流程.json` 打开（`ProductLayout` 先找新外壳 `KeyMouse.Editor.exe`，找不到才退到老外壳）。

第 1 步里那 5 秒不是可选的：**开发树的编辑器连的是 `artifacts\cli\KeyMouse.exe` 这个独立进程**（常驻 Runner 由编辑器拉起，再借它跑流程、开选区浮层）。只重建 `bin` 而不重新发布，编辑器里跑的还是旧引擎——实测踩过：换行输入的解析明明改了，编辑器里跑出来仍然把整段当成一行。改引擎（Core / Runner / CLI）之后必须让 `km e` 把 CLI 也发一遍；只改界面可以 `km e -NoCli` 省掉这 5 秒。

### 权限：编辑器两条路都是提权的，开发时用开关豁免

| 怎么来的 | 清单里的权限 | 用来干什么 |
| --- | --- | --- |
| `.\km e` / `.\km ba`（`dotnet build`） | `requireAdministrator` | 和发布版一致。权限形态本身会影响行为（录不到管理员窗口）：开发时若不带这块，装完才发现录不了 |
| `.\km e -NoElevate`（`dotnet build -p:KeyMouseElevated=false`） | 普通用户（`asInvoker`） | 只调界面时用：不弹 UAC，普通权限的 `KeyMouse` 也能点它自己的按钮 |
| `pack-release.ps1`（`dotnet publish`） | `requireAdministrator` | 装到别人机器上：要能操作、录制以管理员身份运行的窗口（UIPI） |

**别再相信"`dotnet build` 不带清单"这个说法**：`<ApplicationManifest>` 在 build 阶段就会被合并进
apphost，`requireAdministrator` 一样生效。选哪一份清单由属性决定，可以直接问 MSBuild（只求值，不产出）：

```pwsh
dotnet msbuild src\KeyMouse.FlowEditor\KeyMouse.FlowEditor.csproj -getProperty:ApplicationManifest -p:Configuration=Release
#   → app.manifest

dotnet msbuild src\KeyMouse.FlowEditor\KeyMouse.FlowEditor.csproj -getProperty:ApplicationManifest -p:Configuration=Release -p:KeyMouseElevated=false
#   → …\src\KeyMouse.FlowEditor\app.unelevated.manifest
```

`bin\Release\…\KeyMouse.FlowEditor.exe` 里带的是哪一份，取决于最后一次构建：带 `-NoElevate` 编出来的
exe，清单里就没有 `<trustInfo>` 那一段（所以标题栏会如实显示「普通权限」）。
以前文档写着"build 不带清单"、代码里的清单注释又写着"编辑器一律提权"，两者矛盾，直到真的启动一次才查清。

真正需要普通权限的场合是**用自动化验证界面**：提权进程的窗口对普通权限的进程是不可点的
（UIPI 隔离），要拿 `KeyMouse` 点编辑器自己的按钮、或让 UIA 读它的控件树，就必须双方同级。
所以给了个显式开关，而不是让清单按配置分叉——分叉了就等于"开发时验的东西不是发出去的东西"。

```pwsh
.\km e -NoElevate -NoCli    # 普通权限构建，只编编辑器
```

编辑器标题栏显示「构建 <月-日 时:分> · <管理员|普通权限>」，一眼能看出手上这份是哪一种。
提权的已知副作用：系统文件选择器在提权进程里会被拒绝，编辑器自动回退到经典对话框（`Picker.cs`）。

### WinUI 3 运行时

编辑器把 Windows App SDK **自包含**进输出（`WindowsAppSDKSelfContained`）：这台机器上只有系统内置的 CBS 版本，非打包应用用引导程序找不到可用运行时，启动即退出码 `0x80670016`。所以开发机上不需要额外装 Windows App Runtime，发布产物也不要求目标机装。

## 测试

三层，命令都从仓库根跑：

```pwsh
.\km t                      # 单元测试：15 个模块，不需要桌面
.\km t parsing flow         # 只跑改到的模块
.\km tl                     # 列模块
.\km t -n flow              # 不重编，直接跑

.\km s                      # 桌面冒烟：12 个模块，需要解锁的桌面
pwsh tests\smoke\run.ps1 -List
pwsh tests\smoke\run.ps1 -Only typing,features

pwsh tools\diagnostics\evidence.ps1      # 取证：把测量证据归档成一次快照
```

冒烟与取证每次都在 `artifacts\tests\` 下新建一个带时间戳的目录（`smoke-<时间戳>\`、`evidence-<时间戳>\`），互不覆盖；单测跑的是已编译的 exe，不落盘。三层各覆盖什么、什么时候必须跑、写新测试的规矩，见 [testing.md](testing.md)。

## 六道关卡与 CI

`pwsh tools\dev\verify.ps1`（即 `.\km v`）依次跑六道，任一失败即非零退出码：

| # | 关卡 | 实际命令 | 需要桌面 | CI |
| --- | --- | --- | --- | --- |
| 1 | 构建 | `dotnet build KeyMouse.sln -c Release`（0 警告是底线） | ❌ | ✅ |
| 2 | 单元测试 | `tests\KeyMouse.Tests`（15 个模块） | ❌ | ✅ |
| 3 | 文档链接 | `pwsh tools\dev\check-docs.ps1` | ❌ | ✅ |
| 4 | 版本一致 | `pwsh tools\dev\check-version.ps1` | ❌ | ✅（加 `-NoArtifact`） |
| 5 | 打包 | `pwsh tools\build\build.ps1` → `artifacts\cli\KeyMouse.exe` | ❌ | ✅（同一条 publish 命令 + 跑一次 `--version`） |
| 6 | 桌面冒烟 | `pwsh tests\smoke\run.ps1`（12 个模块） | ✅ | ❌（CI 没有交互式桌面） |

`-SkipSmoke` 与 `-SkipPack` 可以跳过第 6、5 关（CI 与赶时间的本地验证用）。锁屏时第 6 关自动跳过并打印原因——判定只看**前台窗口**（`LockApp` / `LogonUI`，或标题匹配「锁屏 / Lock Screen」），不能看「`LockApp` 进程在不在」：解锁之后那个进程常常还留着。

跑完会打印构建的 commit（含「工作区有未提交改动」提示）、产物自报的版本、被跳过的关卡，以及五条值得手动确认的命令：

```pwsh
.\artifacts\cli\KeyMouse.exe --help
.\artifacts\cli\KeyMouse.exe window list
.\artifacts\cli\KeyMouse.exe doctor
.\artifacts\cli\KeyMouse.exe run samples\notepad-demo.txt --dry-run
.\km env
```

CI 侧：`.github\workflows\ci.yml` 在 push 到 `main`、PR 和手动触发时跑第 1–4 关，再用与第 5 关相同的 publish 命令产出 `artifacts\cli` 并执行 `--version`；`.github\workflows\release.yml` 由 `cli-v*` / `gui-v*` 标签触发，多一道「标签里的版本必须等于 `ProductVersion`」，然后重建产物并创建 Release。

## 版本与发布

版本号只有一个来源：根 `Directory.Build.props` 的 `<ProductVersion>`（产品 0.2.1）与 `<EngineVersion>`（引擎 2.2.0）。规则由 `tools\dev\check-version.ps1` 强制：

- `src\KeyMouse.Cli`、`src\KeyMouse.FlowEditor`、`src\KeyMouse.Setup` 用 `<Version>$(ProductVersion)</Version>`；`src\KeyMouse.Core`、`src\KeyMouse.Runner` 用 `$(EngineVersion)`；
- `src\` 下的 csproj / props / targets 不许写死 `<(ProductVersion|EngineVersion|Version)>x.y.z`；
- `.cs` 里不许出现 `const string Version = "数字…"` 这种第二份真相；
- 不传 `-NoArtifact` 时，还会核对 `artifacts\cli\KeyMouse.exe --version` 自报的产品版本与引擎版本。

所以发一次版只需要改 `Directory.Build.props` 一处。

### 打包

```pwsh
.\km ba                                   # 先确认解决方案能编（0 警告）
.\km v                                    # 六道关卡；赶时间可加 -SkipSmoke
pwsh tools\build\pack-release.ps1         # -SkipEditor / -SkipCli 可跳过某一半
```

产物在 `artifacts\release\`：

| 文件 | 说明 |
| --- | --- |
| `KeyMouseSetup-<ProductVersion>.exe` | 安装程序，内含编辑器与命令行两份载荷 |
| `KeyMouse-x64.exe` | 自包含单文件命令行，约 49 MB，目标机什么都不用装 |
| `KeyMouse-fx-x64.exe` | 框架依赖单文件命令行，约 0.3 MB，目标机要装 .NET 10 运行时 |

`pack-release.ps1` 的中间产物也在 `artifacts\` 下（`editor`、`cli`、`cli-self`、`setup`），它可以单独跑，不依赖 `km`——CI 就是这么用的。几条被实测钉住的细节写在脚本注释里：编辑器必须自包含；多余的语言目录只留 `zh-CN`、`en-US`；`.pri` 资源文件必须从 `bin` 里补回发布目录（缺了启动就是 `XamlParseException`）；语言目录不能搬进子目录（WinUI 的 MRT 只认 exe 旁边的目录）。

### 标签与 Release

两个发布系列，标签都带版本号（版本必须等于 `ProductVersion`）：

| 标签 | 附带的产物 | 谁触发 |
| --- | --- | --- |
| `gui-v<版本>` | `KeyMouseSetup-<版本>.exe` | `release.yml` 调 `pack-release.ps1` |
| `cli-v<版本>` | `KeyMouse-x64.exe`、`KeyMouse-fx-x64.exe` | `release.yml` 直接 publish 两份 |

`tools\release\` 下是手动等价物：

```pwsh
pwsh tools\release\release-notes.ps1 -Tag cli-v0.2.1 -Out release-notes.md
pwsh tools\release\release.ps1 -DryRun          # 需要 GITHUB_TOKEN（Contents: write）
pwsh tools\release\clean-releases.ps1 -Remove gui,v0.1.0 -Yes
```

`release.ps1` 自己**不构建任何东西**，它会先跑一遍 `pack-release.ps1`，再按 `ProductVersion` 创建两个 Release。安装、卸载与安装目录布局见 [install.md](../guide/install.md)。

## 代码约定

这几条是踩过坑之后写下来的，改代码之前先读：

1. **一个脚本自始至终一个进程。** `run` 把每一行交给同一个进程内的派发逻辑（`ScriptRunner` 通过委托调 `Program` 的派发），执行器里不许出现 `Process.Start` / `ProcessStartInfo`。唯一例外是感知层显式配置的外部 OCR 引擎（`probe --engine`）——那是"用户配置了一个外部命令"，不是"脚本每行起一个子进程"。
2. **退出码 3 / 4 / 5 / 6 表示一个字节的输入都没发。** 只有它们可以安全重试；退出码 1 是"运行到一半失败"，可能已经发了一半，调用方永远不该自动重试。反过来，任何"可能已经发出输入"的失败都不许报成 3/4/5/6。
3. **静态错误先报。** 解析、选择器、循环结构、变量名这些问题必须在发出第一个事件之前查出来——脚本跑到第 20 行才发现第 3 行写错，比一开始就拒绝糟糕得多。
4. **测试不碰用户的东西。** 冒烟与取证只驱动自己启动的 `tests\KeyMouse.SmokeTarget`（或记事本这类由测试自己开的窗口），不往用户正在用的窗口里发输入；跑完恢复剪贴板、结束自己拉起的进程。
5. **编辑器覆盖层要自己收。** 空状态面板、添加步骤面板这类覆盖层，不能只把里面的内容收起来——留在树上的外壳照样吃掉点击。要连同覆盖层本身一起移除（旧实现用 `ScrollViewer` 包 `EmptyState`，滚走后空白区仍然挡着列表）。
6. **`x:Bind` 默认是 `OneTime`。** XAML 里所有会变的绑定必须显式 `Mode=OneWay`；每一行的状态变化不能靠"属性名为空 = 刷新全部"（编译期绑定按名字精确匹配，发空名字收不到，实测运行结果标记一直不出现），必须走 `StepVm.Refresh` 里那份 26 个属性名的清单。
7. **路径只在 `ProductLayout` 判定一次。** 哪份命令行、哪份编辑器、开发树还是安装版，都只问它；新增一种形态也只改它，别在别处再写一遍 `File.Exists` 链。

另外两条工程习惯：0 警告是底线（第 1 关就是按这个标准过的）；一次提交只做一件事，提交信息写"为什么"而不是"改了什么"。

## 目录

| 路径 | 内容 |
| --- | --- |
| `src\KeyMouse.Core\` | 能力层，按职责分六个命名空间（见下表） |
| `src\KeyMouse.Runner\` | 常驻进程：命名管道、作业、协议（`runner-protocol.schema.json`） |
| `src\KeyMouse.Cli\` | 命令行入口：`Program`、流程编辑器的启动命令、Runner 客户端命令、`app.manifest` |
| `src\KeyMouse.FlowEditor\` | 老外壳：WinUI 3，`MainWindow.*.cs` 五个 partial、`Themes\` 两本资源字典，产物 `KeyMouse.FlowEditor.exe` |
| `src\KeyMouse.EditorUI\` | 新外壳：Tauri + Svelte（`src\lib\*.ts` 纯逻辑带单测、`src-tauri\src\*.rs` 三个文件），产物 `KeyMouse.Editor.exe` |
| `src\KeyMouse.Setup\` | 安装程序：`Program`（入口与参数）、`Log`（安装轨迹）、`Installer`（安装与卸载），载荷在 `Assets\` |
| `tests\KeyMouse.Tests\` | 单元测试（无框架、无依赖的控制台程序，入口 `TestEntry.cs`，按领域分五个目录） |
| `tests\KeyMouse.SmokeTarget\` | 冒烟用的靶子窗口 |
| `tests\smoke\` | 桌面冒烟：`run.ps1`、`lib\common.ps1`、`modules\*.ps1` |
| `tools\dev\` | `km.ps1`、`verify.ps1`、`check-docs.ps1`、`check-version.ps1`、`check-input.ps1`、`editor-ui.ps1` |
| `tools\build\` | `build.ps1`、`pack-release.ps1`、`make-icon.py` |
| `tools\release\` | `release.ps1`、`release-notes.ps1`、`clean-releases.ps1` |
| `tools\diagnostics\` | `evidence.ps1`、`window-levels.ps1`、`screenshot.ps1` |
| `artifacts\` | 所有构建与测试产物（不进 git） |
| `docs\` | 文档（本目录的约定见 [docs/README.md](../README.md)） |

能力层目录就是它的命名空间（每个 csproj 用 `<Using Include="KeyMouse.X" />` 引进来，代码里不必写全名）：

| 目录 | 命名空间 | 管什么 |
| --- | --- | --- |
| `Input\` | `KeyMouse.Input` | 键名与键码、`SendInput` 封装：把一条命令翻译成真实事件 |
| `Windowing\` | `KeyMouse.Windowing` | 窗口枚举、定位、资格闸门、聚焦与还原、低级钩子 |
| `Flow\` | `KeyMouse.Flow` | 流程 JSON 的模型、schema、计划、运行器、录制 |
| `Capture\` | `KeyMouse.Capture` | 截屏与 OCR（`probe`）、区域、文字匹配、取证截图 |
| `Runtime\` | `KeyMouse.Runtime` | 命令分发（`Commands.*`）、脚本文本执行（`ScriptRunner.*`）、用法与执行控制 |
| `Product\` | `KeyMouse.Product` | 产品形态：`ProductLayout`、注册表、`Scratch`、`doctor` |

大文件用 partial 按职责拆开，命名规律是"主文件 + `.职责.cs`"：`Commands.{Target,Mouse,Keyboard,Window,Parsing}.cs`、`ScriptRunner.{Parse,Report}.cs`、`Probe.{Target,Image,Engine,Report}.cs`、`MainWindow.{Document,Steps,Run,Record}.cs`。拆的时候只搬代码、不加抽象——类还是那一个类，调用点一行都不用改。

**还没拆的几个大文件，以及为什么先停手**：

| 文件 | 行数 | 为什么没拆 |
| --- | --- | --- |
| `src\KeyMouse.Core\Flow\Recorder.cs` | ~900 | `Run` 是一段连续的低级钩子循环（装钩子、收消息、合并轨迹、判停止），状态耦合在局部变量里。按行切片只会得到几个共享十几个变量的"函数"，比现在更难读；真要拆得先改设计 |
| `src\KeyMouse.Core\Flow\FlowModel.cs` | ~750 | 类型定义（目标/点/区域/条件/步骤/文档）与加载、校验、占位符展开交错：`Load` 调 `ValidateSteps`，`ValidateSteps` 又调 `CheckPlaceholders`。切点只能落在调用链中间，切完两个文件都得来回跳 |
| `src\KeyMouse.Core\Flow\FlowRunner.cs` | ~640 | 一个执行循环加"每种步骤怎么跑"的分支，分支共享同一份运行状态；拆出去要显式传状态，属于设计改动 |

判断标准：**搬代码不改设计才拆**。拆完要新增抽象、要给一批方法加参数、要在两个文件之间来回跳，就先放着——大本身不是问题，耦合才是。
