# JSON 流程格式

[flow.schema.json](../../src/KeyMouse.Core.Flow.Validation/flow.schema.json)是引擎与编辑器共用的字段契约；`KeyMouse schema` 显示当前 CLI 实际使用的版本。顶层使用 `format: "keymouse-flow"`、`version: 1` 和 `steps`。文档变量放在 `variables`；每一步以 `type` 指定操作，不是 `kind`。

```json
{"format":"keymouse-flow","version":1,"steps":[
  {"type":"set","name":"n","value":"1","delayMs":0},
  {"type":"assert","cond":"n == 1","delayMs":250}
]}
```

## 所有操作的执行前等待

`delayMs` 是非负整数，省略为 1000，填 `0` 立即执行。完全录制的 `durationMs` 文档头是原始录制时长，不覆盖逐步设置。只有旧 `atMs` 时沿用绝对时间线；同时有两者时采用 `delayMs`。拖动的 `durationMs`、长按的 `ms`、打字的 `intervalMs` 均属于动作内部。

## 步骤字段

下表列出类型专属字段。`note`、`shot`、`delayMs` 等公共字段不重复列出；允许单步前提时可附 `when`。未填的可选字段直接省略。坐标相对于客户区时还必须给能定位到唯一窗口的 `target`（进程或窗口类）；不能用只有标题的选择器。拖动两端使用同一坐标空间。

| 步骤 | 必填 | 可选 |
| --- | --- | --- |
| `focus` | `target` | — |
| `click` | `at` | `button`、`target` |
| `drag` | `from`、`to` | `durationMs`、`target` |
| `move` | `at` | `target` |
| `wheel` | — | `delta`、`axis`、`at`、`target` |
| `type` | — | `text`、`intervalMs`、`target` |
| `key` | `combo` | `count`、`target` |
| `hold` | `ms`、`button` / `text` / `combo` 至少填一项 | `at`、`target` |
| `mousedown` | — | `button`、`target` |
| `mouseup` | — | `button`、`target` |
| `keydown` | `combo` | `target` |
| `keyup` | `combo` | `target` |
| `sleep` | — | `ms` |
| `wait-window` | `target` | `timeoutMs`、`intervalMs` |
| `wait-text` | `region`、`text` | `target`、`match`、`maxErrors`、`timeoutMs`、`intervalMs`、`confirm` |
| `click-text` | `region`、`text` | `target`、`match`、`maxErrors`、`timeoutMs`、`intervalMs`、`confirm`、`button` |
| `read-text` | `region`、`into` | `target`、`text`、`match`、`maxErrors` |
| `repeat` | `times`、`steps` | — |
| `foreach` | `in`、`steps` | — |
| `call` | `flow` | `vars`、`export` |
| `if` | `cond`、`steps` | `probe`、`else` |
| `while` | `cond`、`steps` | `probe`、`maxRounds`（默认 200） |
| `do` | `cond`、`steps` | `probe`、`maxRounds`（默认 200） |
| `for` | `cond`、`steps` | `init`、`step`、`probe`、`maxRounds`（默认 10000） |
| `break` | — | — |
| `continue` | — | — |
| `return` | — | — |
| `set` | `name`、`value` | — |
| `assert` | `cond` | `message`、`probe` |
| `mousehold` | `ms`、`button` | `at`、`target` |
| `keyhold` | `combo`、`ms` | `target` |

## 条件、循环与调用

`if` 根据布尔表达式执行 `steps` 或 `else`；`while` 先判断，`do` 至少一次，`for` 依次执行初始化、条件、循环体和步进。循环有 `maxRounds` 上限，运行还可设置 `--max-steps`。`break`、`continue` 只在循环内有效；`return` 结束当前流程而不是整个调用栈。

表达式可以比较值、组合布尔条件或通过 `text()`、`window()`、`front()`、`found()`、`elapsed()` 取样。`text()` 取一次样；若要等到某段字出现，请用 `wait-text` 并设置 `timeoutMs`。`set.value` 是表达式，普通文字步骤的 `text` 是要输入的文本。示例见[控制流程](../../samples/control-demo.json)。

`call.flow` 相对于调用文件，`vars` 向子流程传值，`export` 显式交回变量。循环引用和过深嵌套在加载时拒绝。`read-text.into` 将结果保存在流程变量；报告中的步骤编号是执行顺序，`origin` 才是文档内的位置。

## 坐标和证据

点的 `space` 用 `client` 或显式 `screen`；区域还可用 `window`。取点器同时提供屏幕坐标和可用的相对坐标，选择哪套由步骤字段决定。旧流程应保留自己的空间，不因打开文件就转换。截图路径和 `target.title` 都可能揭示私人内容；公开示例只使用虚构数据。

改契约时同时更新解析与校验、编辑器显示、这张表以及受影响的测试；运行 `pwsh tools/dev/check-docs.ps1`。
