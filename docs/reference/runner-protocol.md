# Runner 命名管道协议

[协议清单](../../src/KeyMouse.Runner/runner-protocol.schema.json)定义方法、参数和事件。它服务于常驻 `KeyMouse serve` 和相应客户端；不是 Tauri 页面使用的[壳 IPC](editor-ipc.md)。传输为限定本机用户的双向命名管道，UTF-8，每行一个 JSON 对象。

连接后的 `id: 0` 问候是服务端主动事件，不是请求应答。发送请求时分配 `id`，用同一 `id` 收集日志、步骤状态和终结事件。一般查询以 `result` 或 `error` 完成；执行类以 `finished` 或 `error` 完成。不能把第一条输出当作完成。

## 请求清单

| 方法 | 专属参数 | 终结事件 |
| --- | --- | --- |
| `watch` | `session`、`after` | `result`、`error` |
| `hello` | — | `result` |
| `status` | — | `result` |
| `list` | — | `result` |
| `shutdown` | — | `result` |
| `run` | `flow`、`dryRun`、`echo`、`keepGoing`、`delayMs`、`retry`、`shots`、`evidenceFor`、`shotDiff` | `finished`、`error` |
| `record` | `out`、`durationMs`、`shots` | `finished`、`error` |
| `validate` | `flow` | `result`、`error` |
| `pick-region` | `mode` | `result`、`error` |
| `ocr` | `process`、`class`、`region` | `result`、`error` |
| `cancel` | `target` | `result`、`error` |
| `pause` | `target` | `result`、`error` |
| `resume` | `target` | `result`、`error` |

请求中的 `delayMs` 属于整次运行选项，不等于 JSON 步骤内的 `delayMs`。兼容客户端可用旧协议；v2 经 `hello` 协商后使用批量状态、断线续收及截图差分能力。`watch` 带上已有会话与游标，不会重新启动作业。

## 事件字段

| 事件 | 字段 |
| --- | --- |
| `state` | `kind`、`id`、`job`、`state` |
| `status-stream` | `kind`、`id`、`cursor`、`value` |
| `screenshot-diff` | `kind`、`id`、`job`、`value` |
| `hello` | `kind`、`id`、`message` |
| `log` | `kind`、`id`、`job`、`line` |
| `step` | `kind`、`id`、`job`、`index`、`total`、`state`、`type`、`shot`、`exitCode`、`durationMs`、`origin` |
| `queued` | `kind`、`id`、`job`、`total`、`message` |
| `finished` | `kind`、`id`、`job`、`exitCode`、`durationMs` |
| `result` | `kind`、`id`、`value` |
| `error` | `kind`、`id`、`code`、`message` |

未知扩展字段应可忽略，未知方法或不兼容版本需要显式错误。详细载荷和协商字段以协议清单为准。运行 `dotnet run -c Release --project tests/KeyMouse.Tests -- --only protocol,protocol-v2` 检查握手、结果和版本兼容。
