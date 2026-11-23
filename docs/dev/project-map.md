# 源码与构建产物地图

在 Windows 的 Release 配置下，先按要修改的能力找项目，再按下表定位产物。`src/` 是产品源码，`tests/` 是验收与靶窗，`tools/` 是复用脚本。`artifacts/` 是本机输出，不属于源码分发。

## 项目、源码与产物

| 项目 | 源码目录 / 重点文件 | 直接责任 | Release 构建输出 |
|---|---|---|---|
| `KeyMouse.Core.Flow.Model` | `src/KeyMouse.Core.Flow.Model/FlowStep.cs` | 流程步骤、目标和坐标 DTO | `src/KeyMouse.Core.Flow.Model/bin/Release/net10.0/KeyMouse.Core.Flow.Model.dll` |
| `KeyMouse.Core.Abstractions` | `src/KeyMouse.Core.Abstractions/` | `ExecuteFlowCommand`、`CommandFailure`；引用 Model | `src/KeyMouse.Core.Abstractions/bin/Release/net10.0/KeyMouse.Core.Abstractions.dll` |
| `KeyMouse.Core.Flow.Expression` | `src/KeyMouse.Core.Flow.Expression/` | 表达式、变量作用域和值 | `src/KeyMouse.Core.Flow.Expression/bin/Release/net10.0/KeyMouse.Core.Flow.Expression.dll` |
| `KeyMouse.Core.Foundation` | `src/KeyMouse.Core.Foundation/{Background,Capture,Input,Product,Runtime,Windowing}/` | Win32 输入/窗口、后台控件、文本脚本、执行控制、路径与注册表 | `src/KeyMouse.Core.Foundation/bin/Release/net10.0-windows/KeyMouse.Core.Foundation.dll` |
| `KeyMouse.Core.Capture` | `src/KeyMouse.Core.Capture/{Capture,Recognition/Ocr}/` | 截图、OCR、区域与文字定位；ONNX Runtime 在此项目引用 | `src/KeyMouse.Core.Capture/bin/Release/net10.0-windows/KeyMouse.Core.Capture.dll` |
| `KeyMouse.Core.Flow.Validation` | `src/KeyMouse.Core.Flow.Validation/Flow/`、`src/KeyMouse.Core.Flow.Validation/flow.schema.json` | 流程文档、契约、校验与编译计划 | `src/KeyMouse.Core.Flow.Validation/bin/Release/net10.0-windows/KeyMouse.Core.Flow.Validation.dll` |
| `KeyMouse.Core.Flow.Execution` | `src/KeyMouse.Core.Flow.Execution/{Flow/IFlowDesktop.cs,Flow/FlowRunner.cs}` | 流程树解释、运行时桌面读取接口 | `src/KeyMouse.Core.Flow.Execution/bin/Release/net10.0-windows/KeyMouse.Core.Flow.Execution.dll` |
| `KeyMouse.Core.Capture.Recording` | `src/KeyMouse.Core.Capture.Recording/Flow/Recorder.cs` | 键鼠钩子会话到流程文档 | `src/KeyMouse.Core.Capture.Recording/bin/Release/net10.0-windows/KeyMouse.Core.Capture.Recording.dll` |
| `KeyMouse.Core` | `src/KeyMouse.Core/Runtime/Commands.cs`、`Commands.Window.cs`、`FlowCommandExecutor.cs`、`Usage.cs`、`src/KeyMouse.Core/Product/Doctor.cs` | 保留稳定程序集入口并组合 Execution / Recording 与 doctor | `src/KeyMouse.Core/bin/Release/net10.0-windows/KeyMouse.Core.dll` |
| `KeyMouse.Runner` | `src/KeyMouse.Runner/` | 常驻进程、作业、协议与命名管道 | `src/KeyMouse.Runner/bin/Release/net10.0-windows/KeyMouse.Runner.dll` |
| `KeyMouse.Cli` | `src/KeyMouse.Cli/` | `KeyMouse.exe` 命令行入口 | `src/KeyMouse.Cli/bin/Release/net10.0-windows/KeyMouse.exe` |
| 新编辑器 | `src/KeyMouse.EditorUI/src/`（Svelte/TS）、`src/KeyMouse.EditorUI/src-tauri/src/`（Rust） | Web UI 与 Tauri shell | `src/KeyMouse.EditorUI/src-tauri/target/release/KeyMouse.Editor.exe`；前端单独输出 `src/KeyMouse.EditorUI/dist/` |
| 旧编辑器 | `src/KeyMouse.FlowEditor/` | WinUI 3 维护/兼容构建，不进入当前安装包 | `src/KeyMouse.FlowEditor/bin/Release/net10.0-windows10.0.26100.0/win-x64/KeyMouse.FlowEditor.exe` |
| Setup | `src/KeyMouse.Setup/` | 把发布载荷写入安装器 | solution build apphost：`src/KeyMouse.Setup/bin/Release/net10.0-windows/win-x64/KeyMouseSetup.exe`；发布暂存/最终包由 `pack-release.ps1` 生成到 `artifacts/setup/` 与 `artifacts/release/` |

测试项目入口：`tests/KeyMouse.Tests/`（.NET 单测；apphost 为 `tests/KeyMouse.Tests/bin/Release/net10.0-windows/KeyMouse.Tests.exe`）、`tests/KeyMouse.E2E/`（CLI E2E）、`tests/KeyMouse.BackgroundProbe/`（后台控件独立验证）、`tests/KeyMouse.UIAccess.Tests/`、`tests/mcp/`、`tests/smoke/` 与 `tests/KeyMouse.SmokeTarget/`。


## 从改动找到命令

| 修改内容 | 构建 | 回归 |
| --- | --- | --- |
| CLI、Core、Runner 或录制 | `dotnet build src/KeyMouse.Cli -c Release` | `dotnet run -c Release --project tests/KeyMouse.Tests` |
| Svelte 与 Tauri 壳 | `pwsh tools/dev/editor-ui.ps1 package` | `pwsh tools/dev/editor-ui.ps1 check`、`pwsh tools/dev/editor-ui.ps1 test` |
| WinUI 兼容项目或 Setup | `dotnet build KeyMouse.sln -c Release` | 安装器需另跑 `pwsh tools/build/pack-release.ps1` 测完整载荷 |
| 文档或流程契约 | `pwsh tools/dev/check-docs.ps1` | 用构建的 CLI 试运行相应示例 |

环境要求见[开发入门](development.md)。独立 CLI、GUI 与安装器的发布目录见[构建工具](../tools/build.md)，桌面验收与无桌面验证的区别见[测试手册](testing.md)。
