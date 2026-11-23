# 开发入门：从干净检出到可运行程序

在 Windows x64 上使用 PowerShell 7 和 .NET 10 SDK。后台控件互操作构建还使用 .NET Framework 4.8 SDK 的 `TlbImp.exe`，非标准安装位置参见 [BackgroundUia.targets](../../tools/build/BackgroundUia.targets)。编译 Tauri 编辑器另需 Node.js、pnpm、Rust/MSVC 与 Windows C++ 构建工具；运行界面需 WebView2。

```powershell
dotnet build src/KeyMouse.Cli -c Release
dotnet run -c Release --project tests/KeyMouse.Tests -- --only timeline,record-focus
pwsh tools/dev/editor-ui.ps1 setup
pwsh tools/dev/editor-ui.ps1 dev
```

只改流程执行或录制时不必启动 GUI。用 `KeyMouse schema` 查看加载的契约，用 `run samples/demo-flow.json` 检查 CLI。改动 Svelte 或 Rust 后分别运行 `editor-ui.ps1 check`、`editor-ui.ps1 test`，需要可分发的编辑器时运行 `editor-ui.ps1 package`。

`dotnet build KeyMouse.sln -c Release` 校验 .NET 解决方案，包含旧 WinUI 项目，但**不会生成 Tauri 编辑器或含完整载荷的安装器**。仓库中的 `bin/` 是项目构建结果，`artifacts/` 是发布暂存和本地测试输出。

下一步：[项目地图](project-map.md)查文件，[测试指南](testing.md)选验证层级，[工具目录](../tools/README.md)找脚本，[公开材料检查](../guide/publication.md)决定哪些文件可以提交。
