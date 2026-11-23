# 维护 Tauri 编辑器

UI 的职责是编辑文档、呈现流程状态及转达操作结果。输入与 OCR 由 CLI 执行。排查时先区分三个边界：Svelte 中的文档状态、Rust 壳的文件与进程操作、CLI 的目标选择与执行。

| 修改位置 | 源码入口 | 验证重点 |
| --- | --- | --- |
| 画布、选中项和撤销 | [App.svelte](../../src/KeyMouse.EditorUI/src/App.svelte)、[前端模块](../../src/KeyMouse.EditorUI/src/lib) | 移动/折叠节点后仍指向原步骤；子文档不会误存到父文件 |
| 参数表单 | [Inspector.svelte](../../src/KeyMouse.EditorUI/src/lib/Inspector.svelte) | 从 `schema` 渲染。数值 `0` 不等于空；旧文件不因打开而被迁移 |
| 运行、录制和文件 IPC | [Rust 命令](../../src/KeyMouse.EditorUI/src-tauri/src) | 参数逐项传递，取消有终态，stdout/stderr 收尾后再通知结束 |
| HUD | [HUD 模型](../../src/KeyMouse.EditorUI/src/lib/hud.ts) | 长流程保留父调用和有限条子记录；旧格式日志仍能显示 |

```powershell
pwsh tools/dev/editor-ui.ps1 check
pwsh tools/dev/editor-ui.ps1 test
pwsh tools/dev/editor-ui.ps1 package
```

`build` 只产生静态资源。端到端排查先确认 `runtime_info` 实际选中了哪个 CLI；Tauri 命令名和数据走向见[IPC 参考](../reference/editor-ipc.md)。WinUI 是不同宿主，不能把它的管道模型用于解释 Tauri 行为。背景见 [ADR 0001](adr/0001-editor-shell-tauri.md)。
