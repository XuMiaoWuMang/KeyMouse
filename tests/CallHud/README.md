# 子流程 HUD 回归夹具

`Program.cs` 用临时构造的父、子流程生成结构化输出；不采集用户桌面。`nested-call.fixture` 是可公开的协议样本，只包含合成的步骤与作用域，不是运行日志归档。前端测试和预览默认读取它。

```powershell
# 从仓库根目录；先安装编辑器依赖
pwsh tools/dev/editor-ui.ps1 setup
node src/KeyMouse.EditorUI/node_modules/vitest/vitest.mjs run --config tests/CallHud/vitest.config.ts
cargo test --manifest-path src/KeyMouse.EditorUI/src-tauri/Cargo.toml hud::
```

需要对照本次 C# 输出时，在新建的临时目录生成 `real-events.log`，仅对本次进程设置 `CALL_HUD_EVENTS` 指向该文件。不要把环境变量指向真实业务日志。

```powershell
$dir = New-Item -ItemType Directory -Path (Join-Path $env:TEMP ([guid]::NewGuid().ToString()))
dotnet run --project tests/CallHud --artifacts-path "$dir/build" -- "$dir/real-events.log"
```

测试逐个播放事件前缀，检查调用进行中、嵌套子项、完成状态和保留上限。`id` 区分每次执行，`scopeId` 表示父调用；`origin` 是文档位置，不能用作运行实例 ID。事件格式见[编辑器 IPC](../../docs/reference/editor-ipc.md)。
