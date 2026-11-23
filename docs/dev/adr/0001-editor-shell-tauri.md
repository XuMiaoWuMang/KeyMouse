# ADR 0001：发布版编辑器采用 Tauri 宿主

**决定：** 用 Svelte/TypeScript 实现流程画布，Rust/Tauri 管理窗口、文件和 CLI 子进程。发布版界面以当前用户运行；需要不同权限的输入能力不应成为整个编辑器的启动条件。

**原因：** 编辑文档、显示报告和保存配置不需要输入权限。将桌面执行留在 CLI，使安装版和独立 CLI 能验证同一份流程，也避免前端复制执行规则。

**代价：** 多了一条跨进程接口，需要处理参数转义、取消、输出排空和启动失败。UI 构建还需要 Rust 与前端工具链，不由 `.sln` 一步完成。

**兼容：** [旧 WinUI 宿主](../../../src/KeyMouse.FlowEditor)保留维护；它使用 [Runner 协议](../../reference/runner-protocol.md)。新壳与 Rust 之间的接口另见[编辑器 IPC](../../reference/editor-ipc.md)。
