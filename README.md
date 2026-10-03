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
```

## 架构：一个产品，两个入口，三层

```
  图形编辑器（editor\KeyMouse.FlowEditor，WinUI 3）      命令行（src\KeyMouse.Cli → KeyMouse.exe）
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
| **目标** | 按标题 / 类名 / 进程 / pid / 句柄选择窗口，`--pick` 消歧义 |
| **闸门** | 拒绝隐藏、最小化、被 DWM cloak、无响应、**被禁用**的窗口 |
| **焦点** | 温和尝试最多 3 次并回读验证；失败即中止，不猜 |
| **坐标** | 绝对像素，或**窗口客户区相对**坐标（窗口移动也不失效） |
| **脚本** | 顺序执行、注释、`sleep`、`waitfor`/`waitgone`、`${变量}`、**目标继承**（`window focus` 写一次，之后 `mouse`/`key` 不再重复选择器）、**循环**（`repeat n [as 名字] … end`）、`--dry-run`、安全重试、JSON 报告 |
| **感知** | `probe` 把窗口的一块区域读成文字：N 次读取**逐字一致**才算看清，置信度、引擎与模型身份一起进 JSON；**读到空也是结果**，不是失败 |
| **选区** | `region pick` 用鼠标框一块区域（或单击选整个客户区），报告它属于哪个窗口的哪个坐标系，并给出一条可直接粘贴的 `probe` 命令；`probe --pick-region` 是"框完直接读" |
| **录制** | `record` 全局监听键鼠（只观察、不拦截），把一次操作写成可回放的 JSON：窗口上下文、客户区相对坐标、抽稀后的轨迹、关键步骤截图；`run flow.json` 回放，闸门与退出码和手打命令一致 |
| **图形编辑器** | `KeyMouse flow edit 流程.json` 打开 WinUI 3 编辑器：左边步骤列表（拖拽排序、缩略图），右边改参数，「从屏幕取区域」直接拉选区浮层，「试运行/播放」调用的还是 `run` 本身 |
| **找字并点它** | `probe --find "取消"` 报告文字**在屏幕上的框与点击点**（客户区坐标）；流程里的 `click-text` 就是"看到就点它的中心"，任何步骤还能带 `when` 前提（不成立则跳过，或按 `else` 失败） |
| **条件步骤** | 流程里可以等：`sleep`、`wait-window`（等窗口可用）、`wait-text`（读一块区域等某段文字出现，容错预算自己声明，连续两次读到同一段才算数，超时退出码 3） |

完整命令与选项 → **[docs/reference.md](docs/reference.md)**
设计取舍、可靠性细节与已知限制 → **[docs/design.md](docs/design.md)**
改动、测试与发布流程 → **[docs/development.md](docs/development.md)**
架构结构树：161 个模块、可下钻的交互式图 → **[normify-keymouse/normify.html](normify-keymouse/normify.html)**

## 安装

**下载**（[Releases](https://github.com/XuMiaoWuMang/KeyMouse/releases)）：

| 文件 | 需要什么 | 体积 |
| --- | --- | --- |
| `KeyMouse-x64.exe` | 什么都不用装 | 约 49 MB |
| `KeyMouse-fx-x64.exe` | .NET 10 运行时 | 约 310 KB |

> 体积主要来自交互式选区浮层需要的 WinForms：自包含产物从 36 MB 长到 49 MB（实测），
> 不用选区的命令一行代码也没变重。见 [docs/design.md](docs/design.md) 的实测记录。

**自己编译**：

```powershell
cd D:\Data\KeyMouse
.\build.ps1                 # 产物 dist\KeyMouse.exe
.\build.ps1 -SelfContained  # 打包运行时，拷到别的机器也能跑
```

想全局调用，把 `dist` 加进 PATH：

```powershell
[Environment]::SetEnvironmentVariable('Path', $env:Path + ';D:\Data\KeyMouse\dist', 'User')
```

## 开发

```powershell
dotnet build KeyMouse.sln -c Release                                          # 构建
dotnet run -c Release --project tests\KeyMouse.Tests\KeyMouse.Tests.csproj    # 单元测试，不需要桌面
pwsh tests\check-docs.ps1                                                     # 文档链接检查
pwsh tests\smoke.ps1 -Exe dist\KeyMouse.exe                                   # 桌面冒烟，需要交互式桌面
pwsh tests\evidence.ps1                                                       # 把一次测量完整留档（本地，不入库）
dotnet build editor\KeyMouse.FlowEditor -c Release                            # 图形编辑器（WinUI 3，单独构建）
```

CI 每次 push 跑前三项；**发布由 `v*` tag 触发，且必须先经过人工验证**——
完整的改动与发布流程见 **[docs/development.md](docs/development.md)**。

## 许可证

**AGPL-3.0**，完整条款见 [LICENSE](LICENSE)。Copyright (C) 2026 XuMiaoWuMang。

可以自由使用、修改、再分发（含商用）；但**分发修改版时必须同样以 AGPL-3.0 开源源码**，
把它做成网络服务时也要向用户提供源码。不提供任何担保。
