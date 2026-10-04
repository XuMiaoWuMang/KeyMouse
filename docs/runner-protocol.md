# 前后端通信协议契约（Runner Protocol）

> 这份文档是**规范**，不是说明。机器可读的那份在
> [`src/KeyMouse.Runner/runner-protocol.schema.json`](../src/KeyMouse.Runner/runner-protocol.schema.json)，
> 一致性由 [`tests/KeyMouse.Tests/RunnerProtocolTests.cs`](../tests/KeyMouse.Tests/RunnerProtocolTests.cs) 强制
> （`dotnet run --project tests\KeyMouse.Tests -- --only protocol`）。
>
> **改协议的顺序是固定的**：先改 schema → 再改两侧代码 → 跑 `--only protocol`。测试红了就是你漏了其中一步。

## 1. 这条链路是谁跟谁说话

```
KeyMouse.FlowEditor（前端）  ──命名管道 JSON 行──▶  KeyMouse serve（后端，常驻）
        KeyMouseBridge.cs                              RunnerHost.cs / RunnerJob.cs
        RunnerClient.cs  ◀──────────────────────────   RunnerProtocol.cs
```

- **前端**：`editor/KeyMouse.FlowEditor`。
- **后端**：`KeyMouse serve` 进程（`src/KeyMouse.Runner`）。
- 两侧共用 `RunnerProtocol.cs` 里的线类型，所以"字段名"这一层天然一致；
  这份契约管的是**上面那层语义**：有哪些方法、每个方法读哪些参数、会回哪些事件、什么时候算结束。
- 也可以被任何会说 JSON 行的脚本直接使用（这是它刻意保持无聊的原因）。

## 2. 传输（不可协商的部分）

| 项 | 值 |
| --- | --- |
| 通道 | 命名管道 `keymouse-runner-{用户名}`（测试用 `keymouse-runner-test-{用户名}-{标签}`） |
| 编码 | UTF-8 |
| 分帧 | 一行一个 JSON 对象；空行忽略；行内不换行 |
| 命名 | camelCase（读时大小写不敏感） |
| 空值 | 写的时候省略 null 字段 |
| 未知字段 | 必须忽略，不得报错 |

## 3. 模型：请求 → 若干事件

客户端发一个请求，服务端**用同一个 id** 回若干事件。id 由客户端选，服务端只负责回显。

**终结事件只有三种**：`result`、`finished`、`error`。客户端收到其中任意一个就认为这次请求结束了。

- `result`：一问一答的方法（`hello` / `status` / `list` / `validate` / `pick-region` / `ocr` / `cancel` / `pause` / `resume` / `shutdown`）。
- `finished`：执行类方法（`run` / `record`），此前会流式回 `log` 与 `step`。
- `error`：任何失败，`code` 与 CLI 退出码同一套语义。

请求形状固定：

```json
{"id": 7, "method": "run", "params": {"flow": "C:\\x.json", "dryRun": true}}
```

事件形状：`kind` 是判别字段，其余字段按种类解释（见第 5 节）。

## 4. 方法表（12 个）

| 方法 | 读取的参数 | 流式事件 | 终结事件 |
| --- | --- | --- | --- |
| `hello` | — | — | `result{version, protocol}` |
| `status` | — | — | `result{version, jobs, pipeline}` |
| `list` | — | — | `result`（作业数组） |
| `run` | `flow`(必填) `dryRun` `echo` `keepGoing` `delayMs` `retry` `shots` `evidenceFor` | `log` `step` | `finished` / `error` |
| `record` | `out` `durationMs` `shots` | `log` `step` | `finished` / `error` |
| `validate` | `flow`(必填) | — | `result{ok, steps, issues}` / `error` |
| `pick-region` | — | — | `result` / `error` |
| `ocr` | `process` `class` `region`(长度 4) | — | `result` / `error` |
| `cancel` | `target` | — | `result{job, state}` / `error` |
| `pause` | `target` | — | `result{job, state}` / `error` |
| `resume` | `target` | — | `result{job, state}` / `error` |
| `shutdown` | — | — | `result{stopping}` |

约定：

- `target` 缺省时取**请求 id 自身**（一次 `run` 的请求 id 就等于它的作业号）。
- `shots` 的缺省按方法分：`record` 缺省为**真**（只有显式 `false` 时才加 `--no-shots`）；
  `run` 缺省为**假**，只有显式 `true` 时引擎才逐步留证据截图（先执行、后截图，落在
  `evidenceFor`（缺省用 `flow` 自己）旁边的 `<流程>.shots/` 里）。
- `echo` 缺省为**真**。
- `region` 必须是长度 4 的数组 `[x,y,w,h]`；长度不对按"没给"处理。
- 不认识的 `method` 必须回 `error(code=2)`，并在 message 里列出可用方法（这份清单由 `RunnerProtocol.Methods` 生成，与契约同源）。
- `client` / `version` 不挑方法：任何请求都可以带，`RunnerClient` 每个请求都带。`version` 与契约版本
  不合时服务端回 `error(code=2)` 并停止处理——**版本先对，再谈方法**。没带 `version` 视为不说，放行。
- 连接后服务端主动发一条 `hello` 问候（id 固定 0）：**它不是应答**，客户端不得 await 它。
  要问版本请发 `hello` 方法，那条回 `result`（见第 5 节）。

## 5. 事件表（6 种）

| kind | 字段 | 说明 |
| --- | --- | --- |
| `hello` | `kind` `id`(=0) `message`(版本号) | **连接后的问候**（契约里的 `greeting`）：不构成应答，客户端不得 await 它 |
| `log` | `kind` `id` `job` `line` | 能力层往 stdout/stderr 打的每一行都会变成一条 |
| `step` | `kind` `id` `job` `index` `total` `state` `type` `shot` `exitCode` `durationMs` | `index` 从 1 开始；`state` ∈ running/finished/failed/skipped；`shot` 是这一步留下的证据截图路径（引擎抓不到就是 `null`，绝不编一个） |
| `finished` | `kind` `id` `job` `exitCode` `durationMs` | 执行类方法的终结事件 |
| `result` | `kind` `id` `value` | 一问一答方法的终结事件，`value` 形状由方法决定 |
| `error` | `kind` `id` `code` `message` | 终结事件；message 面向用户，必须是中文 |

## 6. 参数袋（16 个字段）

参数是一个**扁平袋**：每个方法只读它声明的那几个，其余字段忽略。这样加方法不必加类型，
代价是"哪个方法读什么"必须写在上面的表里——所以 schema 与 `RunnerParameters` 的属性集合
由测试强制相等。

`flow` `out` `dryRun` `echo` `keepGoing` `delayMs` `retry` `durationMs` `shots` `evidenceFor`
`process` `class` `region` `target` `client` `version`

其中 `client` / `version` 是**通用字段**：不挑方法（见第 4 节）。`client` 是"谁在问"，
服务端把它记在作业上，`list` / `status` 看得到；`version` 是"说的哪版协议"，不合就当场拒绝。

## 7. 错误码（与 CLI 退出码同一套）

| code | 含义 |
| --- | --- |
| 0 | 成功 |
| 1 | 运行期错误（含 Runner 停止、返回无法解析） |
| 2 | 用法或请求不合法（未知方法、缺必填、文件不存在） |
| 3 | 没有匹配 / 超时 / 没有这个作业 / 取区域被取消 |
| 4 | 目标不可用 |
| 5 | 焦点未确认 |
| 6 | 读不出来（OCR 无结果） |
| 7 | 被取消 |

## 8. 一致性检查结果（2026-10-04）

`dotnet run -c Release --project tests\KeyMouse.Tests -- --only protocol` 全绿（**27/27**）。除了"清单对不对"，
这份检查还用**实机行为**核对契约说的规则（直接问 `RunnerHost`，不走管道）：

| 检查 | 结果 |
| --- | --- |
| 契约方法 = 后端实现方法 | ✅ 12 = 12 |
| 前端调用的方法 ⊆ 契约 | ✅ 前端调用 5 个（`pick-region` `run` `cancel` `pause` `resume`） |
| 后端能发出的事件种类 ⊆ 契约 | ✅ 6 种 |
| 终结事件只有 result/finished/error | ✅ |
| **每个请求都声明了终结事件** | ✅ 12/12（收口前是 11/12） |
| 执行类方法以 finished/error 终结 | ✅ |
| 问候（`greeting`）不算应答、连上就发 | ✅ 契约写明，`PipeServer` 真的发 |
| 参数袋两侧一致 | ✅ 16 = 16 |
| `client` / `version` 两侧真的在用 | ✅ 客户端无条件带；服务端认版本、把 client 记在作业上 |
| 版本不合被挡下 | ✅ `error(code=2)`，中文说明 |
| `hello` 回显 id、以 `result` 终结 | ✅ 实机：id=42 进，`result#42` 出，带 version/protocol |
| 作业信息形状两侧一致 | ✅ 9 = 9 |
| 已知缺口 | ✅ 空 |

### 2026-10-04 收口的两条缺口（留档）

1. ~~`hello` 不回显请求 id，也不发终结事件~~ → `hello` 现在与别的方法**同一套规则**：回显请求 id，
   以 `result{version, protocol}` 终结。连接后那条 id=0 的问候单独定义成契约的 `greeting`：
   它是问候，不是应答，客户端不得 await 它。
2. ~~`client` / `version` 是没人读写的保留字段~~ → 两侧都用起来了：`RunnerClient` 在每个请求上声明
   `client`（谁在问，取入口程序集名）与 `version`（契约版本）；`RunnerHost` 先对版本再谈方法，
   不合回 `error(code=2)`，并把 `client` 记在作业上（`list` / `status` 看得到）。

> 缺口不留白：`knownGaps.items` 必须为空，这条由测试盯着。真发现差异，先把差异写进 `knownGaps`，
> 再把它的行为钉进测试——要么修掉，要么写下来，不许悄悄存在。
