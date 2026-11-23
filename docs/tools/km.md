# km：仓库内的开发快捷入口

`km.cmd` 调用 [tools/dev/km.ps1](../../tools/dev/km.ps1)，不依赖全局安装的 KeyMouse。在 PowerShell 中从根目录使用 `.\km`。以下是常用动词，脚本头部列出别名与其他选项。

| 命令 | 作用 |
| --- | --- |
| `.\km b` / `.\km ba` | 编译 CLI / 整个 .NET 解决方案 |
| `.\km u` / `.\km ub` / `.\km ut` | 启动 Tauri 开发窗口 / 生成桌面 exe / 跑前端与 Rust 单测 |
| `.\km e` | 构建旧 WinUI 编辑器；不是当前发布版界面的构建命令 |
| `.\km tl` / `.\km t timeline` | 列出无桌面模块 / 只跑指定模块 |
| `.\km d` / `.\km d -s` | 单文件 CLI / 自包含 CLI |
| `.\km env` / `.\km docs` | 查看环境 / 检查文档 |
| `.\km s` / `.\km v` | 桌面冒烟 / 综合验证 |

`r` 会实际运行传入的 CLI 命令，`push` 会操作远端。检查代码和文档时不需要执行它们。具体目录和等价长命令见[项目地图](../dev/project-map.md)。
