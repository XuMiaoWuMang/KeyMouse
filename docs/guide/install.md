# 安装与首次启动

在项目的 [GitHub Releases](https://github.com/XuMiaoWuMang/KeyMouse/releases) 选择 **Windows x64** 附件。需要录制和编辑流程就选安装器；只调用命令行不必安装编辑器。

| 附件 | 适用场景 |
| --- | --- |
| `KeyMouseSetup-<版本>.exe` | 安装编辑器和配套 CLI。编辑器使用 WebView2 运行时 |
| `KeyMouse-x64.exe` | 自包含 CLI，不另装 .NET 运行时 |
| `KeyMouse-fx-x64.exe` | 框架依赖 CLI，需要 .NET 10 Windows Desktop Runtime |

安装器默认写入当前用户的 `%LOCALAPPDATA%\Programs\KeyMouse`；可另选目录。编辑器入口是 `KeyMouse.Editor.exe`，CLI 在其旁的 `cli\KeyMouse.exe`。普通安装和编辑器日常使用不要求管理员权限。独立 CLI 不包含编辑器；OCR 模型是另外的资源，详见 [OCR](../dev/ocr.md)。

首次运行可在 PowerShell 用 `& "$env:LOCALAPPDATA\Programs\KeyMouse\cli\KeyMouse.exe" --version` 检查。若自选了目录，替换为实际位置。找不到命令不等于安装失败：先用完整路径，不假设安装器已改 `PATH`。

通过安装目录中的 `uninstall.exe` 卸载。自己的流程和截图应单独保存，不要放在程序目录。开发树的启动方式另见[环境辨认](environments.md)。
