# 编辑器 IPC：页面、桌面壳与引擎

Svelte 页面通过 Tauri `invoke` 调用 Rust 命令，通过 `shell://…` 事件订阅进度。Rust 负责文件系统和 CLI 子进程；页面不能把启动成功当作流程完成。此接口与 [Runner 命名管道](runner-protocol.md) 独立。

| 场景 | 请求与结果 |
| --- | --- |
| 读取契约与定位工具 | `runtime_info` 给出引擎来源；`flow_schema` 返回当前 CLI 的字段契约 |
| 打开与保存 | `open_flow` / `save_flow` 操作父文件；子文档另有边界与并发保存检查 |
| 取点与读取证据 | `pick_region` 可能返回取消；`read_shot` 只读流程允许的资源 |
| 播放与录制 | 前端先发开始请求，再等输出与终结事件；终结前要排空输出，不用固定延时猜状态 |

命令注册以 [main.rs](../../src/KeyMouse.EditorUI/src-tauri/src/main.rs) 为准。下列字段表是前端与壳的共同约定。

## 输入执行组件

实际播放保持普通权限外壳。仅前台输入流程检测到受保护安装目录中的 `KeyMouse.Input.exe` 时，调用普通 CLI 的 `--uiaccess-run <helper> run ...`。未安装时沿用普通 CLI；安装组件存在但启动、令牌或认证失败时明确报错，不静默回退。试运行及含客户区或子流程调用的流程不启用 UIAccess。桥接标准输入接受 JSON 行 `{"type":"cancel"}` 和 `{"type":"pause","paused":true}`；普通路径继续使用现有停止/暂停机制。输出事件与退出事件保持本文契约，仍须排空 stdout/stderr 后通知退出。

部署、签名与账户限制见 [UIAccess 安装说明](../dev/uiaccess.md)；私有管道与能力诊断见 [执行桥契约](../../tools/dev/uiaccess.md)。编译通过不能替代真实 `TokenUIAccess=1` 与管理员测试窗口验收。

## 三条共同约定

1. **命名**：Rust 侧的结构体一律 `#[serde(rename_all = "camelCase")]`；JS 侧只有 `camelCase`。
   这一条是踩过坑的——snake_case 会让前端读到 `undefined`，而界面会安静地显示"没找到命令行"。
2. **失败**：命令一律返回 `Result<T, ShellError>`，前端拿到的是 `{ code, message }`（见下）。
   界面**不**自己拼错误文案，只把 `message` 原样显示出来，必要时加一句"该找谁"。
3. **不许猜**：外壳不做业务判断（不判断步骤对不对、不判断流程能不能跑），业务判断一律
   问 `KeyMouse.exe`。外壳只做"界面做不到的事"：文件、对话框、进程、环境。

## 环境

### `runtime_info() -> RuntimeInfo`

界面启动时调用一次，之后用于状态行与排障。

```ts
type RuntimeInfo = {
  cliPath: string | null;     // 找到的命令行；null 表示没找到
  cliVersion: string | null;  // 命令行自报的产品版本；没找到时为 null
  environment: 'development' | 'installed' | 'unknown';
  repoRoot: string | null;    // 开发树的仓库根；安装版为 null
  editorExe: string;          // 自己（外壳）的路径
  elevated: boolean;          // 自己是否提权
  initialPath: string | null; // 启动时点名要打开的流程（`keymouse-editor.exe 流程.json`）
};
```

外壳起 `KeyMouse.exe` 时一律带 `KEYMOUSE_OUTPUT_ENCODING=utf-8`：命令行默认跟随控制台码页，
而这里要解析它的文字，猜码页就会把中文解码成 `���`。这条开关写在
[命令行参考 · 输出编码](cli.md#输出编码)。

`cliPath` 的搜索顺序必须与 `src\KeyMouse.Core.Foundation\Product\ProductLayout.cs` 的 `CliCandidates()`
一致，否则会出现"编辑器说找到了、doctor 说没有"的错位：

1. 环境变量 `KEYMOUSE_CLI`
2. `<仓库根>\artifacts\cli\KeyMouse.exe`
3. `<仓库根>\src\KeyMouse.Cli\bin\Release\net10.0-windows\KeyMouse.exe`
4. 自己所在目录
5. 自己所在目录下的 `cli\`
6. `PATH`

`environment` 的判定：从自己所在目录往上找，同时有 `KeyMouse.sln` 与 `Directory.Build.props`
的目录即仓库根，找到就是 `development`，否则 `installed`。

## 文件

### `open_flow(path: string) -> FlowFile`

读文件、按 JSON 解析、把文本与结构一起交给界面。**不做业务校验**（字段对不对由 `validate_flow` 管）。

```ts
type FlowFile = {
  path: string;   // 绝对路径
  text: string;   // 原样文本（保存时以界面的文本为准）
  json: unknown;  // 解析后的对象
};
```

解析失败返回 `code = "flow.invalid-json"`，`message` 带行列号。

### `save_flow(path: string, text: string) -> void`

把界面的文本原样写回文件（UTF-8，无 BOM，LF）。写之前不校验——校验是界面的按钮，
不是保存的前置条件（用户有权保存一份还没写完的流程）。

### `open_child_flow(parentPath, flow, ancestors) -> FlowFile`

父画布展开 `call` 时读真实子文件，参数使用 JS 的 camelCase：

| 参数 | 类型 | 说明 |
| --- | --- | --- |
| `parentPath` | `string \| null` | 调用所在流程的路径；相对引用以它的目录为基准，未保存父流程只能展开绝对引用 |
| `flow` | `string` | `call.flow` 原值；空白或含 `{{` 的动态引用不能在编辑时展开 |
| `ancestors` | `string[]` | 已展开的祖先流程路径；外壳还会检查 `parentPath`，按规范化真实路径拒绝循环 |

返回与 `open_flow` 相同的 `{ path, text, json }`，其中 `path` 是规范化真实路径；`text` 必须保留作子文件保存的比较基线。只解析 JSON，不替代引擎的流程校验。祖先数组长度达到 16 时拒绝继续展开。

失败：`flow.dynamic-reference`（空白/变量引用）、`flow.parent-unsaved`（相对引用缺父路径）、`flow.circular-reference`（祖先循环）、`flow.depth`（展开深度上限）、`shell.io`（定位/读取失败）、`flow.invalid-json`（JSON 解析失败）。界面显示外壳的 `message`，保留父画布和调用节点。

### `save_child_flow(path, text, expectedText) -> void`

| 参数 | 类型 | 说明 |
| --- | --- | --- |
| `path` | `string` | `open_child_flow` 返回的子文件路径 |
| `text` | `string` | 当前子文件草稿的完整 JSON 文本 |
| `expectedText` | `string` | 最近打开或成功保存时的磁盘原文，逐字比较，包含空白和换行 |

先读磁盘原文，比对 `expectedText`，再解析新文本并写子文件。返回成功后，界面才把这次提交文本推进为新的比较基线；失败不得清掉草稿或 dirty，也不得回退为无条件 `save_flow`。

失败：`shell.io`（读写失败）、`flow.conflict`（磁盘原文已改变）、`flow.invalid-json`（新文本不是合法 JSON）。冲突时用户须协调磁盘内容与草稿，再重新打开；当前接口没有强制覆盖参数。此检查是写入前的冲突检查，不是文件锁或原子比较交换。

**独立文档边界**：展开区直接编辑子文件自己的 `steps`、字段与嵌套调用，父 `call` 始终保留 `flow/vars/export` 引用，不把子步骤内联进父 JSON。保存父流程不保存子文件；「保存子流程」只写对应子文件。收起展开区保留草稿，子/孙流程 dirty 或保存中均汇入窗口 dirty；试运行与播放必须等这些草稿保存成功，冲突失败继续拦运行。切换文档/关闭窗口须先确认是否丢弃；父另存为、撤销/重做及会破坏子草稿归属的结构操作须先保护草稿。

### `read_shot(flow_path, shot) -> ArrayBuffer`

读一张录制留下的证据图，返回**原始字节**（界面自己造 `Blob` URL 显示）。

`shot` 就是步骤里 `shot` 字段那一串（相对流程文件，如 `flow.shots/0001-click.png`）。
外壳做三道检查，缺一不可：只认 `.png`；解析后的真实路径必须落在**流程文件所在目录**里
（`..` 和软链接都拦）；文件确实存在。

> 为什么返回字节而不是给 WebView 开文件系统或 asset 协议：这样界面永远不需要懂路径，
> 能被读到的范围就等于"这里放行过的那几张图"。

### `shot_file(flow_path, shot) -> string`

同一张图的**绝对路径**。界面要"用系统看图程序打开"或"在资源管理器里定位"时用它——
界面自己拼不出 PATH 分隔符，也不该拼。检查与 `read_shot` 完全相同。

打开动作本身用 `tauri-plugin-opener` 的 `openPath` / `revealItemInDir`，
能力写在 `src-tauri/capabilities/default.json`（`opener:allow-open-path`、`opener:allow-reveal-item-in-dir`）。

### 单实例与"又一次双击"

外壳只允许一个进程。第二次启动（双击另一个流程文件、或者从命令行再开一次）不会开新窗口：
新进程把意图转给已在跑的那个，然后自己退出。

| 方向 | 名字 | 说明 |
| --- | --- | --- |
| 事件 | `shell://open-external` | `string`：第二次启动时点名要打开的文件绝对路径 |
| 事件 | `shell://close-requested` | 见下一节 |

界面收到 `shell://open-external` 后走和「打开」完全相同的路：有未保存改动先问一句，
问完再 `open_flow`。不要在这里另写一套"直接替换文档"的快速路径——两条路迟早给出两种结果。

为什么用事件而不是在 Rust 里直接读文件：读文件、判断 JSON 合法性、切换文档是界面的事，
Rust 只搬一句话。真正读盘还是 `open_flow`。

## 契约与取点

界面里"这一步有哪些字段、哪个必填、枚举有哪些值"不写在前端：`KeyMouse schema` 就是唯一
来源，老外壳（WinUI）和新外壳读的是同一份。取点/取块同理，浮层与坐标换算都在命令行的
`region pick` 里，外壳不问路径、不算坐标。

### `flow_schema() -> SchemaPayload`

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `json` | `string` | `KeyMouse schema` 的原始输出（整份契约，见 [`interface.md`](interface.md)） |
| `cliPath` | `string` | 是从哪个 `KeyMouse.exe` 问出来的 |

外壳缓存整份 JSON，界面自己解析（`src/lib/schema.ts`）。**不要**在 Rust 里挑字段转发：
契约加一个字段就得改一次壳，等于把"改契约不用改界面"这件事废掉。

**契约的来源是命令行，不是仓库里的文件**：编辑器的 `flow_schema` 每次都去问
`runtime_info.cliPath` 那个 `KeyMouse.exe`。

- 拿着**开发树的** `artifacts\cli\KeyMouse.exe`：它会直接读仓库里的
  `src/KeyMouse.Core.Flow.Validation\flow.schema.json`，改完文件刷新界面就生效，不用重建。
- 拿着**安装版**的 exe，或开发树里那个 `artifacts\cli` 还没重建：读到的是**编译时内嵌**的那一份，
  改源码文件不会影响它。

所以"我改了 schema、界面还是旧值"这件事只有两种可能：跑的是安装版；或者开发树里但
`artifacts\cli` 是旧的——`pwsh tools\build\build.ps1` 之后再看。想确认到底读的哪一份，
看输出面板里的「命令行：…」那一行。

### `pick_region(point: bool) -> PickResult | null`

跑 `KeyMouse region pick [--point] --json`，把结果翻译成界面能直接写进步骤的形状。

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `space` | `'client' \| 'window' \| 'screen'` | 坐标在哪个空间里 |
| `x` `y` `width` `height` | `number` | 区域；取点时宽高也可能是 0 |
| `screen` | `{ x, y, width, height } \| null` | 同一次取点/框选的绝对屏幕坐标，来自 CLI 的 `screen.rect` |
| `process` `title` `class` `handle` | `string \| null` | 选中的窗口（取点也算选中了窗口） |

`Ok(None)` 表示**用户按了 Esc**（命令行退出码 3），不是错误——界面安静地把这次当成"什么
都没发生"。其它非 0 退出码才是错，带命令行原话回给界面。

新建拖动在客户区内取点会绑定目标，两端必须属于同一窗口；跨窗口应切到屏幕坐标。已有客户区目标不被第二次取点覆盖。屏幕坐标取点不会删除用户已有的目标设置。

## 体检与日志

| 命令 | 入参 | 返回 | 说明 |
| --- | --- | --- | --- |
| `run_doctor` | 无 | `string`（整段文本） | 跑一次 `KeyMouse doctor` 并把它的话原样带回来 |
| `crash_log_path` | 无 | `string`（绝对路径） | `%LOCALAPPDATA%\KeyMouse\editor-crash.log`，调用时确保目录存在 |
| `collect_diagnostics` | 无 | `string`（目录绝对路径） | 写 `环境.txt` / `体检.txt` / `崩溃日志尾部.txt` / `设置.json` / `说明.txt` 到 `%LOCALAPPDATA%\KeyMouse\diagnostics\<时间戳>\` |

两条都只"搬话"：环境判断只在命令行里有一份（`ProductLayout` / `Doctor`），界面不复制那套逻辑——
复制的那份迟早会跟命令行分叉。

`collect_diagnostics` 的三条约定：**单份失败只降级**（那一份的名字后面写上"写不进去：…"，其余照写）、
**崩溃日志只取末尾 400 行**（开头那些是几个月前的）、**不联网**（打包完由用户自己决定发给谁）。

## 关窗

外壳不解析流程内容，所以它自己不知道"脏了没有"。界面每次改动后推一次标记，外壳只在
用户点 × 时用它决定"直接关"还是"先问一句"。

| 命令 | 入参 | 出参 |
| --- | --- | --- |
| `set_dirty` | `dirty: boolean` | — |
| `close_window` | — | —（`destroy()`，不再走 `CloseRequested`） |

事件：`shell://close-requested`（无负载）。外壳拦住关窗后发一次，界面确认可以关就调 `close_window`。

那为什么不直接在界面里 `getCurrentWindow().close()`：它会让 `CloseRequested` 再走一遍，
脏标记还在就会被拦回来，于是窗口永远关不掉。

## 日志

界面里没接住的异常（`window.onerror` / `unhandledrejection`）推给外壳，和 Rust 侧的 panic
落在同一个文件：`%LOCALAPPDATA%\KeyMouse\editor-crash.log`（老外壳写的也是这份，
用户那句"看那个日志"换个外壳仍成立）。

| 命令 | 入参 | 出参 |
| --- | --- | --- |
| `log_error` | `message: string` | —（只追加一行 `ui …`，不回报失败） |

它永远不会抛错：记日志本身失败（磁盘满、目录不可写）不该把一次用户操作变成一次崩溃。

### `save_run_log(path: string, text: string) -> void`

输出面板「保存日志…」先使用官方原生 `save()` 对话框（日志 `.log` / 文本 `.txt`），选定路径后调用此命令；取消返回 `null`，不调用命令、不显示错误。命令仅写入用户指定文件，不读取运行报告、崩溃日志或流程文件。

- `path`：用户在保存框中选择的路径；默认名为当前流程文件名加 `-运行日志-<本地时间>.log`，未保存流程为 `未命名流程-运行日志-<本地时间>.log`，不要求先保存流程。
- `text`：点击按钮时输出面板内存中的完整保留快照；逐行文字加 LF，UTF-8 无 BOM。保留当前面板上限（900 行或 40 万字符），不是无限历史归档。运行中同样允许保存，打开对话框后新增的行不计入本次快照。
- 写入：在目标同目录以唯一名字、排他创建临时文件，写完并同步后重命名替换；失败保留旧目标，尽力删除临时文件，返回 `shell.io` 与中文 `message`。不自动创建目标目录。
- 界面：无输出时按钮禁用；保存框或写入期间防重复点击，不依赖流程 dirty / busy / running；成功显示提示，失败原样显示外壳 `message`。不自动保存、不记住导出路径、不改变流程的路径与未保存状态。输出可能含业务内容，由用户决定保存与分享。

实现：`src/KeyMouse.EditorUI/src-tauri/src/run_log.rs`；回归入口为在 `src-tauri` 目录执行 `cargo test run_log::tests`，每例使用唯一临时目录，覆盖 UTF-8 原文、新建/替换、替换受阻时旧内容保全及临时文件清理。

## 跑流程

界面**不判断**流程对不对、窗口在不在——那是 `src\KeyMouse.Core` 的事。外壳只负责起一条
`KeyMouse.exe run <流程> [--dry-run]`，把输出按行转成事件，并在需要时把它掐掉。

| 命令 | 入参 | 出参 |
| --- | --- | --- |
| `validate_flow` | `path` | `RunExit`（同步，跑完才返回） |
| `run_flow` | `path` | —（立刻返回，结果走事件） |
| `run_cancel` | — | `boolean`：有没有东西被掐掉 |
| `write_scratch_flow` | `text` | `string`：临时副本的完整路径 |
| `pause_flow` | `paused: boolean` | `boolean`：确实有在跑的东西被按了 |
| `read_run_report` | — | `string`：上一次运行的报告全文，读不到是空串 |

`read_run_report` 读的是 `%LOCALAPPDATA%\KeyMouse\tmp\editor-run-report.json`——每次运行都带
`--report` 写到那儿，界面在 `run-exit` 之后读它，按报告里的 `origin`（`3`、`6.1`）把每步的
结果写到对应的行上。主路径写入失败时还会尝试 `%TEMP%\KeyMouse\editor-run-report.json`；
读取按修改时间依次尝试两处，只接受可读取的 JSON 对象，目录或残缺 JSON 不会遮住另一份完整报告。
两处都读不到就是空串、界面只留底部输出：报告是补充，不是运行的前提。报告先在同目录写入唯一临时文件，完成后替换目标，失败不会先截断旧报告。

`pause_flow` 只是**置上/清掉**子进程的那个命名事件（`Local\KeyMouse-pause-<pid>`，见
[`cli.md`](cli.md#暂停与继续)）：停点由命令行决定，永远落在两步之间。外壳不自己挂起线程——
挂起会把半个组合键留在键盘上。录制期间不给暂停（那一轮的主语是用户的鼠标，不是流程）。

```ts
type RunExit = { code: number | null; cancelled: boolean };
type RunLine = { stream: 'out' | 'err'; line: string };
```

### 结构化运行日志 `@keymouse-hud/v1`

编辑器启动的引擎在 `KEYMOUSE_HUD=1` 时，向 stdout 写一行前缀加 JSON：`@keymouse-hud/v1 {…}`。外壳仍按原始行转发 `shell://run-output`；主输出面板过滤协议行，HUD 从 `hud_lines() -> string[]` 取快照。普通命令行未设置此变量时保持人读输出。

```ts
type HudEvent = {
  seq: number;                 // 单次运行递增的正安全整数；更新同一 id 必须更大
  id: string;                  // 单次步骤/调用实例的唯一 id，不能是 root
  scopeId: string;             // root，或包含该实例的 call id
  kind: 'step' | 'call';
  origin: string;              // 文档位置；循环复用 origin，但每次执行的 id 不同
  index: number;               // 当前 scope 内按执行实例递增，从 1 起
  total: number | null;        // 动态流程未知总数时为 null，显示 [index]
  command: string;             // 人读动作描述；不包含步骤输出或 call.vars 对象
  status: 'current' | 'done' | 'skipped' | 'failed' | 'cancelled';
  exitCode?: number | null;
};
```

引擎当前使用 `sN` / `cN` 标识步骤/调用实例，`total` 为 `null`。`call` 开始先发 `current`，其子步骤以该调用 id 为 `scopeId`，结束在同一 id 更新最终状态；嵌套调用继续形成层级。协议身份是 `id`，不能用 `origin`、步骤号或调用文件路径合并不同轮次。更新不得改变 `scopeId/kind/origin/index`，旧 `seq` 或非法事件忽略。

HUD 每个根作用域保留最近 **7** 条父日志，每个调用作用域保留最近 **3** 条子日志；调用结束时保留子日志并更新父调用状态，直到它被后续父日志挤出。淘汰调用时递归淘汰其子树；已淘汰父调用的迟到子事件不得复活子树。这里的「结束保留」指调用结束，整次运行退出仍关闭 HUD；下一次运行清空缓冲。没有有效结构化事件时兼容旧 `[x/y]` 文本，原始行缓冲上限 240。`hud_lines` 每 250ms 拉取完整快照，晚挂载不会漏掉开始行。

### 没保存也要能跑

命令行只认文件，但"加两步试试"不该被逼着先存盘。所以界面在**没有路径**或**改过没保存**时，
先把当前编辑内容交给 `write_scratch_flow`，拿回临时副本的路径再去 `validate_flow` /
`run_flow`；跑的是副本，磁盘上正在编辑的那个文件一个字都不动（写回是 `save_flow` 的事）。

三条约定：

- 临时副本固定在 `%LOCALAPPDATA%\KeyMouse\tmp\editor-run.json`——每次覆盖同一个文件，
  用户想去翻也翻得到；拿不到 LOCALAPPDATA 时退到程序所在目录。
- 底部输出必须说明白"跑的是临时副本"，否则用户会以为文件已经跑过了。
- 已保存且没有改动时**不要**写副本：直接跑原文件，行上的编号才对得上。

事件：

- `shell://run-output`（`RunLine`）：每一行原样转发，不裁剪、不改写。
- `shell://run-exit`（`RunExit`）：`cancelled` 为真表示这次是被"停止"掐掉的，
  界面据此说「已停止」而不是「失败」。此事件在 stdout/stderr 读取线程结束、
  HUD 撤除和运行状态清理后发出，保证该次末行输出先于结束事件。

两条约定：

1. **展开的子文件先保存**。父文档可走上面的临时副本；展开区的子/孙流程仍从各自磁盘文件加载，
   所以任一子草稿未保存或保存中，试运行与播放都拦下。保存冲突不会解除拦截。
2. **一次只跑一条**。已经在跑时 `run_flow` 返回错误，不排队、不覆盖。
   停止用 `taskkill /T /F` 杀整棵子树——`run` 可能已经拉起 runner 或外部 OCR 引擎。

为什么不用常驻的 `KeyMouse serve`：那个进程要提权（录制要看见被提权的窗口），
而界面刻意不提权（见 [开发 · 新编辑器外壳](../dev/editor-ui.md)）。一次性子进程的权限
跟着外壳走，行为与在终端里手敲一致，出问题也能拿同一条命令复现。

## 对话框

文件对话框走 Tauri 官方 `dialog` 插件，用法是标准 API：

```ts
import { open, save } from '@tauri-apps/plugin-dialog';
```

- 打开：`open({ filters: [{ name: '流程', extensions: ['json'] }], multiple: false })`
- 另存：`save({ filters: [{ name: '流程', extensions: ['json'] }], defaultPath })`
- 两者取消时返回 `null`，界面**不**当作错误。

## 录制

| 命令 | 参数 | 返回 |
| --- | --- | --- |
| `record_flow` | `outPath`, `durationMs`（0 = 一直录到停），`mode`（`key` 或 `full`；省略沿用完全录制） | —（立刻返回，结果走事件） |
| `record_flow_elevated` | 同上 | —（同上；UAC 里点「是」之后才开始录） |
| `record_stop` | — | `"stopped" \| "killed" \| "idle"` |

录制与播放**共用**同一条子进程通道（同一个 `RunState`、同样的事件 `shell://run-output`
与 `shell://run-exit`），所以界面上"已经在跑"与"正在录"互斥——一次只做一件事。

普通和提权录制均传 `--client-points`。录制及执行含客户区或子流程的流程前，壳核对实际 CLI 的 `schema.capabilities.backgroundClient`，旧引擎不启动。此类流程不交给 UIAccess helper；纯屏幕播放保留原路由。

录制约定：

1. **默认不自我提权**。钩子只看得见与自己同级或更低权限的窗口，提权窗口上的点击录不到；
   命令行会把这件事写进输出（"这些窗口的权限比 KeyMouse 高"），界面原样显示。
   要录那些窗口，走 `record_flow_elevated`（下面第 5 条）。
2. **把自己排除掉**。外壳传 `--ignore-window 0x<hwnd>`（自己的窗口句柄），
   否则"点一下编辑器"也会被录成一步。
3. **普通录制由外壳注册 `Ctrl+Alt+Q`**：`record_flow` 在起子进程前注册全局停止键，收到消息后
   在独立工作线程调用 `record_stop`，避免停止处理与退出时注销热键互相等待；
   `record_stop` 先把 `Local\KeyMouse-record-stop-<子进程 pid>` 置上
   （见 [`cli.md`](cli.md) 的"录制的优雅停止事件"），让录制自己走到"写文件、打印摘要"那一步；
   置不上（老版本命令行没建这个事件）才发一次 `KeyMouse key combo ctrl+alt+q`。
   **提权录制不注册外壳热键**，因为外壳够不着高权限进程，仍由录制进程自己的钩子接收组合键。
   为什么不直接注入组合键：注入的键要落在前台窗口、还要被录制进程的钩子看见；
   录制时前台是用户正在操作的窗口，外壳没有可靠的前台焦点，实测时灵时不灵。
   两条路都是 5 秒还没退出才 `run_cancel` 兜底杀进程
   （代价是这一次录制不落盘，界面会如实说"被掐掉了"）。这 5 秒是留给它落盘的：
   停止键到达之后它要写文件、打印摘要、退出，太短会在写了一半的 json 上打断，
   太长则"停止"按下去要愣好几秒才有反应（1300 步的实录实测不到 1 秒落完）。
4. **结束即打开**：`shell://run-exit` 到达后，若这一轮是录制，外壳读那个文件并切换过去，
   不让用户自己去"打开"。读不出来就说明原因，别让人对着一份空流程猜。
5. **提权录制是另一条路，代价要写在脸上**。`record_flow_elevated` 起的是
   `KeyMouse --elevate record …`：命令行自己用 `runas` 起管理员进程并等它结束，
   所以外壳拿到的仍然是"子进程退出码"（UAC 被拒 = `1223`，录制本身失败 = `1/2/3`）。
   但那个提权进程的权限比外壳高，于是：

   - **停不了**：`record_stop` 送不进键（UIPI）、`run_cancel` 也杀不掉。界面必须自己
     说明"按 `Ctrl+Alt+Q`，或连按两次 `Esc`"，**不要**摆一个按了没用的「停止」；
   - **不进迷你模式**：那条细带上的「停止」同样无效，摆着就是骗人；
   - **结束靠两件事**：`shell://run-exit` 到达，以及那个文件开始存在。

## 窗口形态

录制一开始窗口会缩成一条细带（"迷你模式"），让出屏幕给要录的那个程序；停下就还原。

| 命令 | 参数 | 返回 |
| --- | --- | --- |
| `enter_mini` | — | —（重复调用直接返回） |
| `leave_mini` | — | —（没进过迷你模式时直接返回） |

三条约定：

1. **几何形状是外壳的事**：界面只说"我进/我出"，尺寸、位置、置顶都由外壳记与还原
   （进之前存下宽高、位置、是否置顶，出时按"尺寸 → 位置 → 置顶"还原）。
2. **进得去要出得来**：界面在任何"这一轮录制结束"的路径上都要调 `leave_mini`——
   用户按停止、录制自己到时间、子进程意外退出、关窗、崩溃前能想到的都算。
   漏一条，用户就看着一条细带打不开编辑器。
3. **位置不动**：只变矮，不跳角落。跳位置会让人找不到自己的窗口。

为什么不另开一个"悬浮球"窗口：多一块 webview 在部分机器上不合成（本机实测内容区
始终空白，换 URL、换透明、换创建时机都不行）。取舍记在
[ADR 0001](../dev/adr/0001-editor-shell-tauri.md)。

## 界面自己的账本

最近打开过哪些流程、上次窗口多大——这两件事只影响"下次顺手一点"，跟流程文件没关系，
所以存在外壳这边：`%LOCALAPPDATA%\KeyMouse\editor-settings.json`。**读写都吞错**：账本坏了
就当没有，绝不让编辑器起不来（记不住最近文件不是用户的问题）。

| 命令 | 参数 | 返回 |
| --- | --- | --- |
| `recent_files()` | — | `string[]`：新的在前，**只含仍然存在的文件**（删掉/挪走的条目不再给出来） |
| `remember_recent(path)` | `path` | `string[]`：更新后的列表（界面直接重画） |
| `window_size()` | — | `{ width, height } \| null`：逻辑像素 |
| `save_window_size(width, height)` | 宽、高 | —（宽 < 200 或高 < 120 直接忽略：迷你模式那种细带尺寸记下来毫无意义） |
| `forget_recent(path)` | `path` | `string[]`：去掉这一条之后的列表（比较时按规范化路径，删掉的文件也能去掉） |
| `clear_recent()` | — | `[]`：清空（设置页的「清空最近列表」） |

约定：

1. **记的是哪一份文件就用哪个路径**，不要去重时改成"规范化路径"再显示——用户认的是自己
   打开时的那个名字。
2. 关窗时的尺寸由**外壳**自己记、也由外壳自己恢复（`CloseRequested` 与 `close_window` 两条路都记，`.setup()` 里改窗口尺寸）。界面不必
   关心：界面可能在那一刻已经没了。
3. 列表上限 8 条，界面只显示前 5 条。老外壳也是这个数。

## 分组与长按阈值（也只在本机）

「把流程切成几组」是给眼睛用的：分组**不进流程文件**，否则改一次分组就会把文档弄脏、逼用户保存。
它存在同一个账本里，键是那个流程文件的规范化绝对路径。

| 命令 | 参数 | 返回 |
| --- | --- | --- |
| `load_sections(path)` | 流程文件路径 | `{ at: number, title: string }[]`：`at` = 在第几行（0 起）之前开一组，管到下一个边界为止 |
| `save_sections(path, sections)` | 同上 | 落地之后的数组（外壳会去越界、去重、按 `at` 排序再回写） |
| `long_press_ms()` | — | `number`：当前的长按阈值（毫秒） |
| `save_long_press_ms(ms)` | 毫秒 | `number`：夹到 150–3000 之后**实际生效**的值 |
| `theme()` | — | `string`：账本里的主题 id，认不出时回 `paper` |
| `save_theme(id)` | `paper` / `kraft` / `night` | `string`：实际存下来的 id（不在白名单就落回 `paper`） |
| `theme_options()` | — | `string[]`：外壳认得的主题 id（设置页画选项用，两边只留一份清单） |

三条约定：

1. **`at` 是边界不是成员**：一组 = "从第 `at` 行起，到下一个 `at` 前一行止"。插入/删除步骤时界面自己挪边界
   （`src/lib/sections.ts` 的 `shiftAfterInsert` / `shiftAfterDelete`），外壳不管业务。
2. **`title` 可以是空串**：那是"没有名字的分隔线"，拖走末尾那一组时会出现，用来标出原来那段到哪结束。
3. **改名/挪目录就丢**：键是路径，这是有意的取舍——不要为了保住分组去改用户看到的文件名。

## 错误码

| code | 什么时候 | 界面怎么显示 |
| --- | --- | --- |
| `shell.io` | 读/写文件失败 | 原样显示 `message`（含路径与系统错误） |
| `shell.spawn` | 起 `KeyMouse.exe` 失败 | 原样显示，并提示跑 `doctor` |
| `flow.invalid-json` | 文件不是合法 JSON | 显示行列号 |
| `flow.no-cli` | 需要命令行却没找到 | 显示"没找到命令行"卡片（不是弹窗，见下） |
| `shot.not-png` | 证据图不是 PNG | 显示"这张证据不是 PNG"，不弹窗 |
| `shot.missing` | 证据图不在了（被删或搬走） | 显示"图不在了"，文件名照旧显示 |
| `shot.outside` | 证据图不在流程文件旁边 | 同上，并说明"只认流程文件旁边的 `*.shots/`" |

## 变更前检查

变更请求字段、事件名或取消语义时，同时修改 Rust 生产者与 TypeScript 消费者，并运行 `pwsh tools/dev/editor-ui.ps1 check` 和 `pwsh tools/dev/editor-ui.ps1 test`。运行日志和诊断包是用户数据，测试用虚构路径、窗口与消息，不用私人日志填充协议示例。
