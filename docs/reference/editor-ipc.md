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

### 对话框

文件对话框走 Tauri 官方 `dialog` 插件，用法是标准 API：

```ts
import { open, save } from '@tauri-apps/plugin-dialog';
```

- 打开：`open({ filters: [{ name: '流程', extensions: ['json'] }], multiple: false })`
- 另存：`save({ filters: [{ name: '流程', extensions: ['json'] }], defaultPath })`
- 两者取消时返回 `null`，界面**不**当作错误。

## 校验与运行（P1-3 落地，先写下来）

### `validate_flow(path: string) -> ValidationReport`

起 `KeyMouse.exe run <path> --dry-run`，把它的人话输出按行收集。

```ts
type ValidationReport = {
  exitCode: number;      // 0 = 每一步的窗口都在；3 = 有窗口找不到；其它见命令行参考
  ok: boolean;           // exitCode === 0
  lines: string[];       // 原样输出（中文），界面只负责显示
};
```

### `run_flow(path: string, mode: 'dry' | 'play') -> RunHandle`

- `dry`：`KeyMouse.exe run <path> --dry-run`
- `play`：`KeyMouse.exe run <path>`

输出通过事件流回界面，事件名 `run://output`：

```ts
type RunEvent =
  | { kind: 'line'; text: string }
  | { kind: 'exit'; exitCode: number };
```

### `run_cancel() -> void`

请求停止（杀掉外壳记住的那个子进程）。界面上的「停止」按钮只调这一条。

## 错误码

| code | 什么时候 | 界面怎么显示 |
| --- | --- | --- |
| `shell.io` | 读/写文件失败 | 原样显示 `message`（含路径与系统错误） |
| `shell.spawn` | 起 `KeyMouse.exe` 失败 | 原样显示，并提示跑 `doctor` |
| `flow.invalid-json` | 文件不是合法 JSON | 显示行列号 |
| `flow.no-cli` | 需要命令行却没找到 | 显示"没找到命令行"卡片（不是弹窗，见下） |

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
