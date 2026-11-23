# KeyMouse

KeyMouse 是 Windows 桌面自动化工具。你可以把一次操作录成可编辑的 JSON 流程，也可以手写步骤：等窗口出现、读一行文字、按条件点击，最后用断言判断任务是否完成。图形编辑器用来编排，命令行用来执行和生成报告。

**[下载安装包或独立 CLI](https://github.com/XuMiaoWuMang/KeyMouse/releases)** · [安装指南](docs/guide/install.md) · [编辑器手册](docs/guide/editor.md) · [命令参考](docs/reference/cli.md)

## 先跑一个不碰桌面的例子

在仓库根目录打开 PowerShell。以下命令编译 CLI，然后运行[三步计数流程](samples/demo-flow.json)：从 1 加到 2，再检查结果。不需要打开目标窗口，也不会向当前前台程序发送输入。

```powershell
dotnet build src/KeyMouse.Cli -c Release
$km = '.\src\KeyMouse.Cli\bin\Release\net10.0-windows\KeyMouse.exe'
& $km run samples/demo-flow.json --dry-run
& $km run samples/demo-flow.json
```

## 录制自己的任务

1. 打开编辑器，选「关键录制」来保留任务动作，或选「完全录制」来保留动作间隔与抽稀后的鼠标轨迹。按 `Ctrl+Alt+Q` 停止。
2. 选中一个动作，在「执行前等待（毫秒）」里调整节奏。关键录制每步默认等 1000 毫秒；完全录制填实际间隔；填 `0` 即立即执行。等待跟着步骤移动，不占一行。
3. 先试运行以检查窗口和参数，再在可控的桌面播放。**客户区鼠标动作**可发给符合条件的后台窗口；屏幕坐标和普通键盘输入不是后台输入。见[边界与排障](docs/guide/troubleshooting.md)。

录制文件可能包含输入原文、窗口标题和截图。仓库提供的示例是合成数据；自己的流程请存入 `local-recordings/` 或工作区以外。提交或反馈问题前请看[公开材料检查](docs/guide/publication.md)。

## 从源码开发

| 目标 | 源码 | 命令（从仓库根目录） | 主要输出 |
| --- | --- | --- | --- |
| CLI 与共用引擎 | [CLI](src/KeyMouse.Cli)、[能力组合层](src/KeyMouse.Core) | `dotnet build src/KeyMouse.Cli -c Release` | `src/KeyMouse.Cli/bin/Release/net10.0-windows/KeyMouse.exe` |
| 桌面编辑器 | [Svelte 与 Tauri](src/KeyMouse.EditorUI) | `pwsh tools/dev/editor-ui.ps1 setup` 后运行 `pwsh tools/dev/editor-ui.ps1 package` | `src/KeyMouse.EditorUI/src-tauri/target/release/KeyMouse.Editor.exe` |
| 无桌面测试 | [测试入口](tests/KeyMouse.Tests/TestEntry.cs) | `dotnet run -c Release --project tests/KeyMouse.Tests` | 控制台给出通过数与失败项 |
| 发布附件 | [打包脚本](tools/build/pack-release.ps1) | `pwsh tools/build/pack-release.ps1` | `artifacts/release/` |

[开发环境与依赖](docs/dev/development.md) · [完整源码和产物地图](docs/dev/project-map.md) · [测试指南](docs/dev/testing.md) · [文档目录](docs/README.md)。许可证：[AGPL-3.0](LICENSE)。
