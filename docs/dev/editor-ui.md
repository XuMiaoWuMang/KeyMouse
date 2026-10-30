# 新编辑器外壳（Tauri + Svelte）

`src/KeyMouse.EditorUI/` 是流程编辑器的新外壳，用来替换 `src/KeyMouse.FlowEditor`（WinUI 3）。
**两个都在仓库里**：老的那套照常能编能跑，新的这套按 [ADR 0001](adr/0001-editor-shell-tauri.md) 的阶段推进，
在功能对齐之前，谁也不是"能当真用的那个"。

## 为什么另起一个目录

- 老外壳的界面进程要管理员权限（为了录得到提权窗口的点击）。新外壳**永不提权**：界面只负责画和发指令，
  需要权限的动作交给 `KeyMouse.exe`（录制、注入、读屏）。
- 老外壳的打包被 WindowsAppSDK 拖着走（`.pri`、96 个语言目录、bootstrapper）。新外壳只有 WebView2。
- 两套并存是刻意的：新壳已经能开、能改、能跑、能录，但还缺拖拽排序、右键菜单、自绘标题栏，
  WinUI 版在这些补齐之前仍是"功能更全的那个"；删早了会丢掉一条退路。

## 目录

```
src/KeyMouse.EditorUI/
  index.html            挂载点，只引 /src/main.ts
  package.json          pnpm 脚本：dev / build / check / tauri
  vite.config.ts        开发服务器固定 5183；Svelte 的 TS 预处理在这里开
  tsconfig.json         独立配置，不继承外部预设
  src/
    main.ts             挂 App 到 #app，引入主题
    App.svelte          整屏界面（顶栏 / 工具栏 / 两栏 / 底部输出）
    lib/theme.css       主题 var：纸、墨、朱砂、分隔线、字阶、圆角
    lib/model.ts        流程文档的类型（界面用得着的子集）与"这一步在做什么"的措辞
  src-tauri/
    Cargo.toml          只依赖 tauri / serde
    tauri.conf.json     窗口 1338×949、devUrl 5183、bundle 暂关
    capabilities/default.json
    src/main.rs         开窗口、找命令行、报环境（唯一的系统交互入口）
```

## 第一次构建

前置：Node ≥ 20、pnpm 11、Rust stable（`cargo --version` 能跑）、WebView2 运行时（Win11 自带）。

```powershell
pwsh tools\dev\editor-ui.ps1 setup      # pnpm install（默认走 npmmirror）
pwsh tools\dev\editor-ui.ps1 dev        # 编 Rust + 起 vite + 开窗口
```

这个脚本把三处环境坑收在一处（npm 源、cargo 的证书吊销检查、pnpm 的 build 脚本批准），
细节见 [`editor-ui.ps1`](../tools/editor-ui.md)。

只想起前端、不开窗口：

```powershell
pwsh tools\dev\editor-ui.ps1 build      # 产物在 dist/，约 17 kB js + 10 kB css
pwsh tools\dev\editor-ui.ps1 check      # svelte-check：0 error 0 warning 才算过
```

## P0 现在是什么样

已经有的：

- 一屏完整的界面：顶栏（标记 + 文件名）、工具栏四个常用步骤 + 录制/试运行/播放、左栏步骤列表（串点的那条线、
  序号、图标、标题、一行摘要、分组步的步数、子步骤 `6.1`）、右栏检查器、底部输出条。
- 图标是一套自绘的 16×16 描边图形（`src/lib/StepIcon.svelte`），按"这一步在做什么"画，不按字段画。
- 底部状态行显示 `KeyMouse 0.2.1（引擎 2.1.0）· 开发树`，来自 `runtime_info`——也就是它真的找到了命令行；
  找不到时显示一行"没找到命令行 KeyMouse.exe"。
- 界面进程不提权：状态行那颗点是朱砂色（普通权限）；提权时会变成警示色。
- **能打开真实流程**（P1a）：`新建` / `打开` / `保存` / `另存为`，文件对话框走 Tauri 的 dialog 插件；
  命令行点名 `KeyMouse.Editor.exe 流程.json` 时启动即载入。
- **能改字段**：检查器里的进程名 / 窗口类名 / 标题 / 备注写回文档，改动后顶栏出现「未保存」，
  `保存` 变为可用；会丢东西的动作（关掉未保存的文档）才弹确认框，其余都写进底部那行。
- 底部那行提示是"轻提醒"：普通的成功只更新一行字，只有失败才展开「运行输出」面板；
  那个面板是页面的一部分（占一行），不是浮层——浮层在窄窗口里正好盖住出问题的列表。

- **能加能删能排序**（P1b）：工具栏四个常用按钮各加一步，`+ 更多` 按用途列出剩下十一种；
  行尾浮出 ↑ ↓ ×，键盘 `Delete` 删、`Alt+↑/↓` 移、`↑/↓` 选行；
  `Ctrl+Z` / `Ctrl+Shift+Z` 撤销重做（撤销栈存的是改动前的文档 JSON，不是对象引用）。
- **新加的步骤是半成品**：`makeStep()` 只放"这一步是什么"，参数留空；缺必填项的步骤在列表里
  标「待补」，检查器右上角写「还缺 文字」这类人话。规则表照抄
  [`interface.md`](../reference/interface.md) 的参数契约，前端不重复实现校验。
- **关窗会问一句**：改动没保存时点 × 先确认（外壳拦 `CloseRequested`、发事件、界面答完再关）。
- 快捷键：`Ctrl+S` 保存、`Ctrl+O` 打开、`Ctrl+N` 新建、`Esc` 收起「+ 更多」。
- **能跑起来了**（P1c）：`试运行` 跑 `KeyMouse.exe run <流程> --dry-run`（只做资格检查，
  不发送任何输入），`播放` 跑同一条命令的真身；输出按行落到「运行输出」面板，
  跑着的时候播放按钮换成「停止」，按 `Esc` 也能停（`taskkill /T /F` 杀整棵子树）。
  跑完底部那一行说结论：`每一步都成功` / `没跑通（退出码 3）：窗口不满足条件` /
  `已停止。`——退出码翻译在 `src\lib\run.ts`，有单测。
- **跑的时候知道跑到第几步**：从命令行输出里读 `[ 3/8 ]`（`src\lib\progress.ts`），
  底部显示「跑到 第 3/8 步」。不去猜"是列表里第几行"——`run` 的编号是执行序号，
  循环会展开、子流程会内联，硬对行号会给出一个自信但错误的答案。
- **能录了**（P2）：`录制` 先问你存到哪（默认"当前流程名-录制.json"），然后起
  `KeyMouse.exe record --out <文件> --ignore-window 0x<自己的句柄>`；录着的时候按钮变成朱砂的
  「录制中」，`停止` 发一次命令行自己的停止键 `Ctrl+Alt+Q`（1.5 秒没退再兜底杀），
  结束后自动把文件打开。**不自我提权**：按当前权限录，提权窗口上的点击录不到，
  命令行会把这句话写进输出。

还没有的：

- 拖拽排序、把子步骤拖进/拖出分组（现在只能在分组末尾追加，见 P1c 之后再定）。
- 录制的"提权窗口"路线：现在只能按外壳的权限录；要录管理员窗口，得先用
  `KeyMouse --elevate record`（这个取舍写在 ADR 的未决问题里）。
- 列表里的"正在跑第几步"高亮：现在只有底部那一行数字，还没有把高亮落到行上。
- 右键菜单与步骤复制粘贴。
- 自绘标题栏（现在用系统标题栏）。

## P3：它已经进安装包了

新壳叫 **`KeyMouse.Editor.exe`**，和老的 `KeyMouse.FlowEditor.exe` 并列装在同一个目录里，谁也不改名、
谁也不覆盖谁。`ProductLayout` 找编辑器时**同一个目录先找新壳、找不到再看老壳**；注册表 `GuiPath`
记的是实际存在的那一份（新壳优先），开始菜单快捷方式也指向它。等哪天老壳退役，删掉 `flowedit.zip`、
从 `ProductLayout` 的编辑器名字列表里去掉那个名字就行，别的不用动。

打包链路（`pack-release.ps1` → 安装程序）：`pnpm tauri build --no-bundle` → `target\release\KeyMouse.Editor.exe`
（约 3 MB，界面资源已内嵌）→ `artifacts\editor-ui\` → 单条目 `editorui.zip` → 安装时解到 `<安装目录>\`。

**WebView2 是唯一的系统依赖**：Tauri 不自带浏览器内核。安装程序会查注册表里的
`EdgeUpdate\Clients\{F3017226-…}`，缺了就在安装目录写一份 `先读我-需要-WebView2.txt`，安装照常完成
（命令行不受影响）。它**只查不装**：把 100 MB 的离线包装进安装包，或者偷偷联网下引导器，都不是安装程序
该替用户决定的事。

**顺带修的一处卸载缺陷**：以前卸载只认注册表与一个固定的用户目录；注册表没写成（被策略挡了、被清理过）
时，`uninstall.exe` 会"成功"退出却一个文件都不删。现在它会先看自己所在目录——它本来就住在安装目录里，
那里有 `KeyMouse.Editor.exe` 或 `KeyMouse.FlowEditor.exe` 就敢整棵删。

**本会话没能验证的一件事**：装到仓库外面（`%LOCALAPPDATA%\…`）在本机被 ACL 沙箱挡住，所以"安装版布局"
（编辑器在 `<安装目录>\`、命令行在 `<安装目录>\cli\`）只验证到"候选里有这两条"（`layout` 单元测试）与
"装在仓库里时仍按开发树解析"。真机验证要在正常权限下装一次。

## P4：收尾

- **单实例**：`tauri-plugin-single-instance` 必须作为**第一个**插件注册（后注册的话，第二个实例在它拦下之前
  就已经跑起来了）。第二次启动不再开新窗口：把已有窗口 `unminimize` + `set_focus`，再把点名的文件用
  `shell://open-external` 交给界面打开。实测 `KeyMouse.Editor.exe a.json` 已在跑，再启动 `… b.json`：
  只剩一个进程，窗口里已经是 `b.json`。
- **崩溃日志**：`src-tauri\src\crashlog.rs` 把 Rust panic 与界面里没接住的异常都追加到
  `%LOCALAPPDATA%\KeyMouse\editor-crash.log`——和老外壳同一个路径，用户那句"看那个日志"仍然成立。
  时间戳手写（UTC、固定 20 字符，字典序即时间序），不为此引第三方库。
- **证据图**：录制时引擎会把每一步"当时看到的样子"存成 `<流程>.shots\NNNN-<类型>.png`，步骤里记的是
  相对路径（`shot` 字段）。界面按需向外壳要字节，自己造 `Blob` URL 显示——**不给 WebView 开文件系统权限、
  也不用 asset 协议**，能被读到的范围就等于外壳放行过的那几张图（`read_shot` 的三道检查见
  [外壳契约](../reference/editor-ipc.md)）。位置：右侧检查器最下方「证据」一节（缩略图 + 文件名 +
  「看大图」「在文件夹里看」），列表里带证据的那一行右侧有一个 ▣ 记号。
  为什么缩略图只有 140px 高：再高就会把检查器其它字段顶到滚动外面，而它们是改这一步时要同时看见的。
  要不要点开由用户决定（点击交给系统看图程序，不是自己再写一个预览窗）。
- **录制时够得着停止**：录制一开始把主窗口缩成 420×52 的细带（`src-tauri\src\mini.rs` 的
  `enter_mini` / `leave_mini`），位置不动——只变矮，不跳角落。细带里只有抓手、`● 正在录制`、
  `已录 N 秒`、一句提示和朱砂「停止」。停下（按钮、`Esc`、录制自己到时间、子进程意外退出）都要还原，
  漏一条用户就看着一条细带打不开编辑器。
  为什么不是另开一个悬浮球窗口：试过了，本机多开的那块 webview **内容区永远空白**
  （`WebviewUrl::App`/`External`、透明与否、创建时机、连 `data:text/html` 都白），老外壳的球是
  自绘 Win32 窗口所以不受影响。取舍与理由记在 [ADR 0001](adr/0001-editor-shell-tauri.md) 的已解决一节。
- 还没做的（写在 ADR 的未决问题里）：录制的提权路线、拖拽排序、行内"跑到第几步"高亮。


## 检查器由契约画出来（P5）

老外壳的检查器是"读 `flow.schema.json` 再按 kind 选控件"，新外壳照做，字段一个都不写死：

- `src/lib/schema.ts` 解析 `KeyMouse schema` 的输出（`FieldContract` / `StepContract` / `ShapeContract`）。
  形状是 `{ comment, fields }` 而不是数组——第一版按数组写，运行时炸在
  `shapeFields(...).every is not a function`，这条教训留在函数注释里。
- `src/lib/Inspector.svelte` 按 kind 渲染：`text`/`multiline`/`int`/`bool`/`enum`/`target`/
  `point`/`region`/`when`；`showWhen`（`button` / `!button`）决定二选一步骤里哪个字段出现。
  契约把 `note`/`when`/`shot` 放在 `common` 里，所以每种步骤都把 common 拼进来——`shot` 例外，
  证据图有自己的块。
- `src/lib/fieldpath.ts` 负责按 `when.timeoutMs` 这种点号路径读写：写空值等于删键，删空的
  嵌套对象连父键一起去掉，返回新对象（撤销栈是序列化快照，原地改会让快照跟着变）。
- 老的"待补"判定（`lib/steps.ts`）还在，用于快速加步骤时给个提醒；检查器里的必填标来自契约的
  `required`。
- 取点/取块走 `pick_region`（`src-tauri/src/pick.rs`）：外壳只把结果写成坐标 + 顺手填
  `target.process`，浮层与坐标换算仍在命令行的 `region pick` 里。

## 界面与老外壳的关系

P0 的目标是"两屏并排看不出这是两个程序"：

- 顶栏：`▞` 标记 + `KeyMouse` + `流程编辑器` + 当前文件路径（居中，省略号在左侧）。
- 工具栏：常用步骤四个（点击、输入文字、等文字出现、找字并点它）+ `+ 更多`；右侧 `录制`（圆点）/`试运行`/`播放`（朱砂实心）/`···`。
- 两栏：左侧步骤列表（序号、图标片、标题、一行摘要、分组步的步数、子步骤 `6.1`），右侧检查器（按契约渲染：目标窗口、这一步、前提条件、备注、证据、JSON 折叠）。
- 底部：状态行 + 输出胶囊；点开是等宽字体的运行输出。

配色与间距取的是 `src/KeyMouse.FlowEditor/Themes/Palette.xaml` 的同一套 token，`theme.css` 里以 `--kp-*` 变量落地。

## 与命令行的接口

界面不碰文件、不起进程；跨过去只有这些命令，参数与错误码都写在
[编辑器接口契约](../reference/editor-ipc.md)：

| 命令 | 入参 | 出参 |
| --- | --- | --- |
| `runtime_info` | — | `{ cliPath, cliVersion, environment, repoRoot, editorExe, elevated, initialPath }` |
| `open_flow` | `path` | `{ path, text, json }`；JSON 不合法报 `flow.invalid-json` |
| `save_flow` | `path`, `text` | —（会自己建父目录） |
| `flow_schema` | — | `{ json, cliPath }`：整份契约 |
| `pick_region` | `point` | `{ space, x, y, width, height, process, title }` 或 `null`（用户取消） |
| 对话框 | 前端直接调 `@tauri-apps/plugin-dialog` | 取消返回 `null`，不算失败 |

`cliPath` 的查找顺序与 `ProductLayout.CliCandidates()` 一致：`KEYMOUSE_CLI` → `<仓库>\artifacts\cli` →
`src\KeyMouse.Cli\bin\Release\net10.0-windows` → 自己目录 → 自己目录\cli → PATH。
`environment` 是 `development` / `installed`，判定规则见 [开发树与安装版](../guide/environments.md)。

## 名字容易混的两个"编辑器"

| 路径 | 是什么 |
| --- | --- |
| `src\KeyMouse.FlowEditor\` | 老外壳：WinUI 3（C#/XAML），完整功能，界面进程提权，产物 `KeyMouse.FlowEditor.exe` |
| `src\KeyMouse.EditorUI\` | 新外壳：Tauri + Svelte（Rust），不提权，产物 `KeyMouse.Editor.exe` |

`km e` 编的是前者；后者用 `pwsh tools\dev\editor-ui.ps1 build`（底层是 pnpm 与 cargo）。
