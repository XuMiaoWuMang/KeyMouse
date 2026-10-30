# 编辑器与外壳之间的接口契约

新编辑器外壳（`src\KeyMouse.EditorUI`）分成两半：

- **界面**：Svelte 页面，跑在 WebView2 里。它不会碰文件、不会起进程、不会调 KeyMouse。
- **外壳**：Rust 写的 `src-tauri`，负责窗口、文件对话框、读写文件、起 `KeyMouse.exe`。

两半之间只有这一份契约。界面里每次跨过去，都必须能在下面找到对应的一节；反过来，
下面每一条都必须有实现（写在这里而没实现的，算缺陷）。

老编辑器（`src\KeyMouse.FlowEditor`）仍然是进程内直接调 Core，与这份契约无关。

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

`cliPath` 的搜索顺序必须与 `src\KeyMouse.Core\Product\ProductLayout.cs` 的 `CliCandidates()`
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

### `pick_region(point: bool) -> PickResult | null`

跑 `KeyMouse region pick [--point] --json`，把结果翻译成界面能直接写进步骤的形状。

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `space` | `'client' \| 'window' \| 'screen'` | 坐标在哪个空间里 |
| `x` `y` `width` `height` | `number` | 区域；取点时宽高也可能是 0 |
| `process` `title` `class` `handle` | `string \| null` | 选中的窗口（取点也算选中了窗口） |

`Ok(None)` 表示**用户按了 Esc**（命令行退出码 3），不是错误——界面安静地把这次当成"什么
都没发生"。其它非 0 退出码才是错，带命令行原话回给界面。

界面拿到结果后做三件事：写坐标字段、把 `process`/`title` 一并填进这一步的 `target`
（只有坐标不知道是哪块屏幕，回放到别的窗口上就偏了）、底部写一句"取了客户区上的
120,64（notepad）"这类人话。

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

## 跑流程

界面**不判断**流程对不对、窗口在不在——那是 `src\KeyMouse.Core` 的事。外壳只负责起一条
`KeyMouse.exe run <流程> [--dry-run]`，把输出按行转成事件，并在需要时把它掐掉。

| 命令 | 入参 | 出参 |
| --- | --- | --- |
| `validate_flow` | `path` | `RunExit`（同步，跑完才返回） |
| `run_flow` | `path` | —（立刻返回，结果走事件） |
| `run_cancel` | — | `boolean`：有没有东西被掐掉 |

```ts
type RunExit = { code: number | null; cancelled: boolean };
type RunLine = { stream: 'out' | 'err'; line: string };
```

事件：

- `shell://run-output`（`RunLine`）：每一行原样转发，不裁剪、不改写。
- `shell://run-exit`（`RunExit`）：`cancelled` 为真表示这次是被"停止"掐掉的，
  界面据此说「已停止」而不是「失败」。

两条约定：

1. **要有文件才能跑**。命令行读的是磁盘上的 JSON，界面里没保存的改动它看不见；
   与其偷偷写临时文件让报错行号对不上界面，不如直接提示先保存。
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
| `record_flow` | `outPath`, `durationMs`（0 = 一直录到停） | —（立刻返回，结果走事件） |
| `record_stop` | — | `"stopped" \| "killed" \| "idle"` |

录制与播放**共用**同一条子进程通道（同一个 `RunState`、同样的事件 `shell://run-output`
与 `shell://run-exit`），所以界面上"已经在跑"与"正在录"互斥——一次只做一件事。

四条约定：

1. **不自我提权**。钩子只看得见与自己同级或更低权限的窗口，提权窗口上的点击录不到；
   命令行会把这件事写进输出（"这些窗口的权限比 KeyMouse 高"），界面原样显示。
   要录那些窗口，用命令行 `KeyMouse --elevate record --out …`。
2. **把自己排除掉**。外壳传 `--ignore-window 0x<hwnd>`（自己的窗口句柄），
   否则"点一下编辑器"也会被录成一步。
3. **停止用产品自己的停止键**：`record_stop` 发一次 `KeyMouse key combo ctrl+alt+q`，
   让录制自己走到"写文件、打印摘要"那一步；3 秒还没退出才 `run_cancel` 兜底杀进程
   （代价是这一次录制不落盘，界面会如实说"被掐掉了"）。这 3 秒是留给它落盘的：
   停止键到达之后它要写文件、打印摘要、退出，太短会在写了一半的 json 上打断。
4. **结束即打开**：`shell://run-exit` 到达后，若这一轮是录制，外壳读那个文件并切换过去，
   不让用户自己去"打开"。读不出来就说明原因，别让人对着一份空流程猜。

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

约定：

1. **记的是哪一份文件就用哪个路径**，不要去重时改成"规范化路径"再显示——用户认的是自己
   打开时的那个名字。
2. 关窗时的尺寸由**外壳**自己记、也由外壳自己恢复（`CloseRequested` 与 `close_window` 两条路都记，`.setup()` 里改窗口尺寸）。界面不必
   关心：界面可能在那一刻已经没了。
3. 列表上限 8 条，界面只显示前 5 条。老外壳也是这个数。

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

## 界面约定（给写界面的人）

- **打开就打开**：`initialPath` 非空时启动即载入，不让用户再点一次「打开」。

- **轻提醒优先**：普通结果、可恢复的失败写进底部「运行输出」，不弹对话框。
  只有"会丢东西"的动作（关闭未保存的文档、覆盖已有文件）才用 `dialog` 的确认框。
- **不自己造错误文案**：`shell.*` 的 `message` 是给人看的，原样显示；需要补充的只有"下一步怎么办"。
- **状态行**：左边一颗点 + `KeyMouse <产品版本>（引擎 <引擎版本>）· <开发树/安装版>`；
  命令行没找到时点变警示色并写一句话。提权时点也变警示色（界面进程本不该提权）。

## 变更流程

加一条命令的顺序是固定的，反过来做会留下没人管的接口：

1. 改这份文档（写清楚参数、返回值、错误码、界面怎么用）
2. 改 `src-tauri/src/main.rs`（或按职责拆开的 `commands/*.rs`）
3. 改界面
4. 跑 `pwsh tools\dev\editor-ui.ps1 check` 与 `build`

## 相关

- 外壳的目录与阶段：[新编辑器外壳](../dev/editor-ui.md)
- 流程文件本身长什么样：[流程格式](interface.md)
- 命令行的退出码：[命令行参考](cli.md)
