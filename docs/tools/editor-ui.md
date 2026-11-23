# 构建与测试编辑器

[editor-ui.ps1](../../tools/dev/editor-ui.ps1) 在 `src/KeyMouse.EditorUI/` 下执行相应工具。第一次开发先运行 `pwsh tools/dev/editor-ui.ps1 setup`。后续命令只需将 `setup` 替换为下列动词：

| 动词 | 结果 |
| --- | --- |
| `dev` | 启动交互式开发窗口 |
| `check` / `test` | Svelte 类型检查；Vitest 和 Rust 单测 |
| `build` | 前端资源写入 `src/KeyMouse.EditorUI/dist/`，没有可运行的桌面 exe |
| `package` | 生成 `src/KeyMouse.EditorUI/src-tauri/target/release/KeyMouse.Editor.exe` |
| `icons` | 从产品图标源图生成 Tauri 图标 |
| `clean` | 移除本地依赖与中间产物；下次需要重新安装/编译 |

安装依赖可通过 `-Registry` 指定可访问的镜像。调试时确认 CLI 也来自本次构建，参见[环境辨认](../guide/environments.md)。工具输出位于 `artifacts/tests/editor-ui/`，不是源码。
