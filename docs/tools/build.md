# 构建、发布暂存与安装器

编译成功只说明程序可以链接，不说明安装器携带了完整载荷。按需要选择以下入口：

| 命令 | 主要输出 | 用途 |
| --- | --- | --- |
| `dotnet build src/KeyMouse.Cli -c Release` | `src/KeyMouse.Cli/bin/Release/net10.0-windows/` | 本地调试，运行目录需配套 DLL |
| `pwsh tools/build/build.ps1` | `artifacts/cli/KeyMouse.exe` | 框架依赖的单文件 CLI |
| `pwsh tools/build/build.ps1 -SelfContained` | `artifacts/cli-self/KeyMouse.exe` | 自包含 CLI |
| `pwsh tools/dev/editor-ui.ps1 package` | `src/KeyMouse.EditorUI/src-tauri/target/release/KeyMouse.Editor.exe` | 独立构建新编辑器 |
| `pwsh tools/build/pack-release.ps1` | `artifacts/release/` | 构建并暂存版本化安装器和发布附件，不上传 |

[完整打包脚本](../../tools/build/pack-release.ps1)准备编辑器与 CLI、生成 `src/KeyMouse.Setup/Assets/` 下的 ZIP，再发布 Setup。旧 WinUI 编辑器不进入普通安装包。`-SkipEditor`、`-SkipCli` 会复用已有产物，正式发布须先核对版本。需要内置识别时先[准备模型](fetch-ocr-models.md)。

可选签名组件须单独[审核与部署](../dev/uiaccess.md)。核对预计附件可运行 `pwsh tools/build/pack-release.ps1 -Plan`；打包目录中不应包含个人签名材料或本机运行报告。
