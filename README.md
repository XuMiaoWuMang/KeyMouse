# KeyMouse

[![ci](https://github.com/XuMiaoWuMang/KeyMouse/actions/workflows/ci.yml/badge.svg)](https://github.com/XuMiaoWuMang/KeyMouse/actions/workflows/ci.yml)

Windows 命令行输入模拟工具：**一条命令 = 一次真实的鼠标/键盘事件**，执行完就退出。
没有常驻进程、不装驱动——底层是 Win32 `SendInput`，事件进的是系统输入队列，
和应用收到的真人操作走同一条路。

它和常见自动化脚本的区别在于**能指定目标窗口，并且动手之前先验证**：
窗口不可用、或者抢不到前台，就**一个字节都不发**。

反过来，它也能**只读不写**：`probe` 把窗口的一块区域读成文字（OCR），
同样先验证目标，读不清就以退出码 `6` 失败——**只报告看到什么，判断权在调用方**。

```powershell
KeyMouse key type "hello 世界" --process notepad     # 打字前先确认记事本可用且已聚焦
KeyMouse mouse click left -wx 200 -wy 300 --title "记事本"
KeyMouse run script.txt                              # 一批命令顺序执行
KeyMouse record --out flow.json                      # 录一段操作（Ctrl+Alt+Q 停）
KeyMouse run flow.json                               # 原样回放，或改完再放
KeyMouse doctor                                      # 判不清环境时先跑这个
```

## 架构：一个产品，两个入口，三层

```
  图形编辑器（src\KeyMouse.FlowEditor，WinUI 3）        命令行（src\KeyMouse.Cli → KeyMouse.exe）
                 │ 命名管道 + JSON 行：run/record/pick-region/ocr/validate/cancel/pause/resume/status
                 ▼
  常驻 Runner（src\KeyMouse.Runner，KeyMouse serve）
    ├─ 作业注册表：排队、暂停/继续/取消、逐步事件、日志流
    └─ 驱动同一份派发（Commands.Execute）
                 ▼
  能力层（src\KeyMouse.Core）：流程解释器、SendInput、全局钩子/录制、OCR/读屏、取区域、窗口闸门、流程 JSON
                 ▼
  Windows API / 离线 Tesseract 模型 / 流程文件
```

- **两个入口**：编辑器连常驻 Runner；CLI 既能一次性执行（`KeyMouse run flow.json`），也能驱动同一个 Runner（`KeyMouse runner run flow.json`）。**两个入口跑同一份执行体**，退出码、闸门与焦点验证的语义不会分叉。
- **三层**：能力层不认识 IPC，Runner 不认识 UI，UI 不认识流程格式细节（它引用 Core，不复制 schema）。Runner 通过 Core 的 `Execution.Control` 接缝实现暂停/取消/逐步上报；CLI 不装这个接缝，同一段代码就是一次普通阻塞调用。
- **常驻的理由**：作业状态、逐步事件、暂停/取消、日志流都是进程内状态——一次性子进程给不了。

## 能力一览

| | |
| --- | --- |
| **输入** | 鼠标移动 / 点击 / 双击 / 按住 / 滚轮 / 拖拽；键盘敲击 / 组合键 / Unicode 打字（中文不依赖输入法） |
| **目标** | **可选**：不指定窗口就把事件直接发到屏幕坐标／当前焦点（不校验、不抢前台）；指定了才按标题 / 类名 / 进程 / pid / 句柄选窗口，`--pick` 消歧义 |
| **闸门** | 只对**指定了窗口**的步骤生效：拒绝隐藏、最小化、被 DWM cloak、无响应、**被禁用**的窗口 |
| **焦点** | 温和尝试最多 3 次并回读验证；失败即中止，不猜 |
| **坐标** | 绝对像素，或**窗口客户区相对**坐标（窗口移动也不失效） |
| **脚本** | 顺序执行、注释、`sleep`、`waitfor`/`waitgone`、`${变量}`、**目标继承**、**循环**（`repeat n [as 名字] … end`）、`--dry-run`、安全重试、JSON 报告 |
| **感知** | `probe` 把窗口的一块区域读成文字：N 次读取**逐字一致**才算看清，置信度、引擎与模型身份一起进 JSON；**读到空也是结果**，不是失败 |
| **选区** | `region pick` 用鼠标框一块区域（或单击选整个客户区），报告它属于哪个窗口的哪个坐标系，并给出一条可直接粘贴的 `probe` 命令 |
| **录制** | `record` 全局监听键鼠（只观察、不拦截），把一次操作写成可回放的 JSON：屏幕坐标、抽稀后的轨迹、关键步骤截图 |
| **图形编辑器** | `KeyMouse flow edit 流程.json` 打开 WinUI 3 编辑器：账本式步骤列表、按契约渲染的参数检视器、浮层取点/取块、试运行与播放、运行态逐步回写、录制 |
| **找字并点它** | `probe --find "取消"` 报告文字**在屏幕上的框与点击点**；流程里的 `click-text` 就是"看到就点它的中心"，任何步骤还能带 `when` 前提 |
| **循环与变量** | 流程可以带 `variables`、用 `{{名字}}` 传值、用 `repeat` / `foreach` 循环；`read-text` 把屏幕内容读进变量 |
| **子流程** | 一个流程可以 `call` 另一个流程文件：内联执行、变量作用域链式、`export` 回传；成环、缺文件、超过 8 层在加载时就被拒 |
| **条件步骤** | `sleep`、`wait-window`、`wait-text`（读一块区域等某段文字出现，连续两次读到同一段才算数，超时退出码 3） |

## 安装

**下载**（[Releases](https://github.com/XuMiaoWuMang/KeyMouse/releases)）：

| 文件 | 需要什么 | 体积 | 适合 |
| --- | --- | --- | --- |
| `KeyMouseSetup-<版本>.exe` | 什么都不用装 | 见发布页 | 想用图形编辑器的人 |
| `KeyMouse-x64.exe` | 什么都不用装 | 约 49 MB | 只想用命令行 |
| `KeyMouse-fx-x64.exe` | .NET 10 运行时 | 约 0.3 MB | 已经装了运行时 |

安装版把编辑器放在 `<安装目录>\`、命令行放在 `<安装目录>\cli\`，并写
`HKCU\Software\KeyMouse`（`GuiPath` / `CliPath`）。细节见 **[docs/guide/install.md](docs/guide/install.md)**。

> 自包含产物体积主要来自交互式选区浮层需要的 WinForms（36 MB → 49 MB 实测），
> 不用选区的命令一行代码也没变重。见 [docs/dev/design.md](docs/dev/design.md)。

**自己编译**：

```powershell
.\km d                      # → artifacts\cli\KeyMouse.exe（框架依赖单文件）
.\km d -s                   # → artifacts\cli-self\KeyMouse.exe（自带运行时）
.\km e                      # 编编辑器，并把命令行重新打到 artifacts\cli（编辑器读它）
```

想全局调用，把 `artifacts\cli` 加进 PATH：

```powershell
[Environment]::SetEnvironmentVariable('Path', $env:Path + ';D:\Data\KeyMouse\artifacts\cli', 'User')
```

## 仓库结构

```
km.cmd                    根目录唯一入口（转调 tools\dev\km.ps1）
Directory.Build.props     版本号唯一来源：ProductVersion / EngineVersion
src\                      全部出货代码
  KeyMouse.Core\            能力层
  KeyMouse.Runner\          常驻 Runner
  KeyMouse.Cli\             命令行入口
  KeyMouse.FlowEditor\      WinUI 3 编辑器
  KeyMouse.Setup\           安装程序
tools\                    开发自动化（不出货）
  dev\                      日常入口 km、全部关卡 verify、check-docs、check-version
  build\                    打包单文件、打发布三件套
  release\                  发 GitHub Release、生成发布清单、清理旧 tag
  diagnostics\              读屏取证归档、窗口与权限清单
tests\                    单元测试、冒烟测试（含靶子程序）
docs\                     guide / reference / dev / tools
artifacts\                全部构建产物（不入库）
```

分层理由：**src 出货、tools 帮开发、tests 验证**；三者不混放，脚本不再往根目录堆。

## 开发

```powershell
.\km b                     # 只编 CLI（Core + Runner），~1s
.\km ba                    # 编整个解决方案（含编辑器与安装程序）
.\km t                     # 全部单元测试（不需要桌面）
.\km t parsing             # 只跑改到的模块（可写多个）
.\km tl                    # 列出单元测试模块
.\km s                     # 桌面冒烟（需要解锁的交互式桌面）
.\km v                     # 全部关卡：构建 / 单测 / 文档 / 版本 / 打包 / 冒烟
.\km env                   # 环境判定：开发树还是安装版，命令行与编辑器各在哪
.\km                       # 不带参数：打印动词表
```

完整流程（含发布与标签）见 **[docs/dev/development.md](docs/dev/development.md)**，
每个脚本的说明见 **[docs/tools/README.md](docs/tools/README.md)**。

## 文档

| | |
| --- | --- |
| 全部文档索引 | [docs/README.md](docs/README.md) |
| 命令与选项 | [docs/reference/cli.md](docs/reference/cli.md) |
| 流程 JSON 与前后端契约 | [docs/reference/interface.md](docs/reference/interface.md)、[docs/reference/runner-protocol.md](docs/reference/runner-protocol.md) |
| 开发树与安装版的区别 | [docs/guide/environments.md](docs/guide/environments.md) |
| 设计取舍与已知限制 | [docs/dev/design.md](docs/dev/design.md) |

## 许可证

**AGPL-3.0**，完整条款见 [LICENSE](LICENSE)。Copyright (C) 2026 XuMiaoWuMang。

可以自由使用、修改、再分发（含商用）；但**分发修改版时必须同样以 AGPL-3.0 开源源码**，
把它做成网络服务时也要向用户提供源码。不提供任何担保。
