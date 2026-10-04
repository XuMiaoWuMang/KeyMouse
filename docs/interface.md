# 接口文档：JSON 参数契约

这份文档规定 KeyMouse 对外接口里**哪些 JSON 参数是必须的、哪些是可选的**，以及前端（WinUI 编辑器）
该怎么用它们。规矩只有一条：

> **前端只依据契约渲染，加载器只依据契约校验。两边都不许自己另写一套"哪种步骤有哪些字段"。**

以前正是各写一套，然后脱节：`click-text`（找字并点击它）在格式里要有区域、文字、匹配方式、上限、按钮，
界面上却只剩"目标 + 备注"可以改。现在契约是唯一的真相，改契约 = 改一个文件，界面自动跟着变。

---

## 1. 契约在哪、怎么看

| 项 | 位置 |
| --- | --- |
| 契约文件（唯一权威） | `src/KeyMouse.Core/flow.schema.json` |
| 分发方式 | 作为资源**内嵌进 KeyMouse.Core.dll**，跟着程序走，不依赖文件路径 |
| 命令行查看全部 | `KeyMouse schema` |
| 命令行看一种步骤 | `KeyMouse schema click-text` |
| 单元测试 | `dotnet run --project tests\KeyMouse.Tests -c Release -- --only flow` |

`KeyMouse schema click-text` 的输出就是前端能拿到的信息，例如：

```
click-text：找字并点击它
  target         必须                     目标窗口
  region         必须                     在哪里找
  text           必须                     要找的文字
  match          可选                     匹配方式  取值：contains | exact | fuzzy
  maxErrors      可选                     容错字符数  范围：0 .. 20
  timeoutMs      可选                     最多等（毫秒）  范围：0 .. 600000
  button         可选                     鼠标键  取值：left | right | middle
  note           可选                     备注（只给人看）
  when           可选                     前提条件
```

## 2. 契约里每个字段的含义

| 键 | 含义 | 前端拿它做什么 |
| --- | --- | --- |
| `name` | 参数名（就是 JSON 里的键） | 读写这个参数 |
| `kind` | 该用什么控件渲染（见下表） | 选控件；`point`/`region`/`target`/`when` 是**嵌套结构**，继续问 `shapes` |
| `required` | `true` = **必须给** | 标"必须"，为空时提示 |
| `requiredWhen` | 只在某种情况下必须（如"坐标是客户区时必须给窗口"） | 标"条件必须（…）" |
| `default` | 省略时程序采用的值 | 提示"留空 = 默认"；可选字段留空就是**不写进 JSON** |
| `min` / `max` | 整数范围 | 限制输入（NumberBox 的上下限） |
| `values` | 枚举取值 | 下拉框的选项 |
| `label` / `help` | 中文/英文标题与说明 | 字段标题与说明文字 |

`kind` 取值与对应控件：

| kind | 控件 | 说明 |
| --- | --- | --- |
| `const` | 只读文字 | 固定值（如 `format` 必须是 `keymouse-flow`） |
| `text` | 单行输入框 | |
| `multiline` | 多行输入框 | 如 `type` 的要输入文字 |
| `int` | 数字框（带上下限） | 留空 = 用默认值 |
| `bool` | 勾选框 | |
| `enum` | 下拉框 | 选项来自 `values`；第一项是"（默认）"，选中它 = 不写这个参数 |
| `point` | 子卡片：`space/x/y` | 可配"从屏幕取一个点" |
| `region` | 子卡片：`space/x/y/width/height` | 可配"从屏幕取区域" |
| `target` | 子卡片：`process/class/title` | `process` 与 `class` **至少给一个** |
| `when` | 子卡片：前提条件字段 | 空文字 = 没有前提（这一步总是执行） |
| `steps` | 提示"在「这一步的 JSON」里编辑" | 分组步骤的子步骤 |
| `vars` | 提示同上 | `call` 传进去的变量表 |
| `stringList` | 提示同上 | `call` 的 `export` 名字数组 |

## 3. 文档级参数（流程文件本身）

| 参数 | 必须 | 类型 | 说明 |
| --- | --- | --- | --- |
| `format` | **是** | 字符串 | 必须等于 `keymouse-flow` |
| `version` | **是** | 整数 | 目前只认 `1` |
| `steps` | **是** | 数组 | 至少一步 |
| `allowRestore` | 否 | 布尔 | 默认 `false`。允许还原最小化窗口（不写就不还原，退出码 4 拒绝） |
| `variables` | 否 | 对象 | 值是字符串，或字符串数组（给 `foreach` 遍历）；步骤里用 `{{名字}}` 引用 |
| `recordedAt` | 否 | 字符串 | 录制时间，只给人看 |

## 4. 步骤级参数一览（必须 = M，可选 = O）

公共字段（每种步骤都可以有）：`note`(O)、`when`(O)、`shot`(O)。

| 步骤 | 必须给的参数 | 可选参数 |
| --- | --- | --- |
| `focus` | `target` | `note`、`when` |
| `click` | `at` | `button`、`target`（坐标是客户区时必须）、`note`、`when` |
| `drag` | `from`、`to`、`target` | `durationMs`、`note`、`when` |
| `move` | `at` | `target`（客户区时必须）、`note`、`when` |
| `wheel` | — | `delta`、`at`、`target`（客户区时必须）、`note`、`when` |
| `type` | — | `text`、`intervalMs`、`target`、`note`、`when` |
| `key` | `combo` 或 `text` **之一** | `count`、`target`、`note`、`when` |
| `sleep` | — | `ms`、`when` |
| `wait-window` | `target` | `timeoutMs`、`intervalMs`、`note`、`when` |
| `wait-text` | `target`、`region`、`text` | `match`、`maxErrors`、`timeoutMs`、`intervalMs`、`confirm`、`note`、`when` |
| `click-text` | `target`、`region`、`text` | `match`、`maxErrors`、`timeoutMs`、`intervalMs`、`confirm`、`button`、`note`、`when` |
| `read-text` | `target`、`region`、`into` | `text`、`match`、`maxErrors`、`note`、`when` |
| `repeat` | `times`、`steps` | `note` |
| `foreach` | `in`、`steps` | `note` |
| `call` | `flow` | `vars`、`export`、`note` |

关于 `when`（前提条件）：

- 它**不是必须的**。`text` 为空时整个 `when` 被忽略——**等于没有前提**，这一步每次都执行。
- `when` 自己的必须字段：`text`、`region`；其余（`target`/`match`/`maxErrors`/`timeoutMs`/`intervalMs`/`confirm`/`else`）可选。
- 语义上和 `click-text`/`wait-text` 的 `text` **不冲突**：`when` 在步骤开始前只读一次，决定"做不做"；
  步骤自己的 `text` 是它会一直等到超时的目标。

关于 `region`：只支持 `space: "client"`（客户区坐标）——屏幕坐标会因窗口移动失效。
**高度要盖住整行文字**，否则字被切掉会读错（实测：`0,0,400,20` 读成乱码，`0,0,400,32` 正常）。

关于占位符 `{{名字}}`：只能出现在**字符串**字段（文字、组合键、进程名/类名/标题、备注等）。
JSON 的数字放不下占位符，这是限制不是疏忽。

## 5. 常驻 Runner 的 IPC 接口（JSON 行协议）

命名管道 `keymouse-runner-<用户名>`，一行一个 JSON 对象：请求 `{"id":1,"method":"run","params":{...}}`，
事件 `{"kind":"hello|log|step|finished|result|error", ...}`。`id` 与 `method` **必须**，`params` 视方法而定。
规范的全文在 [`runner-protocol.md`](runner-protocol.md)，机器可读的那份是 `src/KeyMouse.Runner/runner-protocol.schema.json`：

| method | params 必须 | params 可选 | 说明 |
| --- | --- | --- | --- |
| `hello` | — | — | 回显请求 id，回 `result{version, protocol}`。连接后服务端还会主动发一条 **id=0 的问候**（`kind: hello`）——那不是应答，别 await 它 |
| `status` | — | — | 服务状态与作业表 |
| `list` | — | — | **作业**表（不是窗口列表；窗口请用 `window list`） |
| `run` | `flow` | `dryRun`、`echo`、`keepGoing`、`delayMs`、`retry`、`shots`、`evidenceFor` | 执行一个流程；事件逐步上报；`shots` 为真时引擎逐步留证据（落 `<流程>.shots/`） |
| `record` | — | `out`、`durationMs`、`shots` | 录制一段操作 |
| `validate` | `flow` | — | 只校验，不执行 |
| `pick-region` | — | — | 交互式选区，返回 `region` 与窗口 |
| `ocr` | `region` | `process`、`class` | 读一块区域 |
| `cancel` / `pause` / `resume` | `target`（作业号） | — | 控制正在跑的作业 |
| `shutdown` | — | — | 让 Runner 退出 |

任何请求都可以带上 `client`（谁在问）与 `version`（说的哪版协议）：`version` 与契约版本不合时服务端回
`error(code=2)` 并停止处理；`client` 会记在作业上，`list` / `status` 看得到。

**作业保证**：任何请求都会有结论——`finished` 一定来，失败时先来 `error` 说明原因。
（这条是踩出来的：校验曾经发生在拿到队列槽之前，一抛异常作业就永远停在 `queued`，客户端只能干等。）

## 6. 改动契约的正确流程

1. 改 `src/KeyMouse.Core/flow.schema.json`（加字段、改必须性、改范围、改说明）。
2. 跑 `--only flow`：测试会检查"每种步骤类型都有契约条目""每个字段的 kind 编辑器都能渲染"。
3. 界面**不需要改**：编辑器每次选中步骤时按契约重建检查器。
4. 若新参数是程序要执行的（不只是给人看），在加载器/执行器里用它，并在 `docs/reference.md` 补一句语义。

相关文档：`docs/reference.md`（命令行与语义细节）、`docs/design.md`（为什么这样设计）。
