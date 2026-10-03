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
```

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

完整命令与选项 → **[docs/reference.md](docs/reference.md)**
设计取舍、可靠性细节与已知限制 → **[docs/design.md](docs/design.md)**
改动、测试与发布流程 → **[docs/development.md](docs/development.md)**
架构结构树：129 个模块、可下钻的交互式图 → **[normify-keymouse/normify.html](normify-keymouse/normify.html)**

## 安装

**下载**（[Releases](https://github.com/XuMiaoWuMang/KeyMouse/releases)）：

| 文件 | 需要什么 | 体积 |
| --- | --- | --- |
| `KeyMouse-x64.exe` | 什么都不用装 | 约 36 MB |
| `KeyMouse-fx-x64.exe` | .NET 10 运行时 | 约 240 KB |

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
```

CI 每次 push 跑前三项；**发布由 `v*` tag 触发，且必须先经过人工验证**——
完整的改动与发布流程见 **[docs/development.md](docs/development.md)**。

## 许可证

**AGPL-3.0**，完整条款见 [LICENSE](LICENSE)。Copyright (C) 2026 XuMiaoWuMang。

可以自由使用、修改、再分发（含商用）；但**分发修改版时必须同样以 AGPL-3.0 开源源码**，
把它做成网络服务时也要向用户提供源码。不提供任何担保。
