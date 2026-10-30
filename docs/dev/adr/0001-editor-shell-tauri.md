# ADR 0001：编辑器外壳换成 Tauri，界面进程不再持有管理员权限

- 状态：**已采纳**（P0 外壳跑通、P1 契约层与增删排序、P2 录制与试运行/播放、P3 打进安装包——新壳
  `KeyMouse.Editor.exe` 与老壳并列安装、旧名字保留；界面进程始终不提权。P4 收尾未开始）
- 日期：2026-10-29
- 相关：[开发树与安装版](../../guide/environments.md)、[开发](../../dev/development.md)、[runner 协议](../../reference/runner-protocol.md)

## 背景：WinUI 3 那套卡在哪

现在的编辑器是 `src/KeyMouse.FlowEditor`（WinUI 3 + C#），能跑，但三个代价越来越明显：

1. **整个界面进程要管理员权限。** 为了录得到提权窗口的点击，`app.manifest` 里写了 `requireAdministrator`，
   于是"画界面的进程"和"能操作别的窗口的进程"是同一个。任何界面层的 bug（解析一个畸形 JSON、渲染一张坏图）
   都是在最高权限下发生的。开发时还得为此专门做一个"不提权清单"绕过去（`km e -NoElevate`）。
2. **打包是被 WindowsAppSDK 拖着走的。** `.pri` 文件要手工补、96 个语言目录要删、缺 bootstrapper 就报
   `0x80670016`、单文件发布要开 `WindowsAppSDKSelfContained`。这些都在 `tools/build/pack-release.ps1` 里
   留下了一串补丁式注释。
3. **样式表达力有限。** 这套"工程纸"要靠一堆 `ControlTemplate` 与主题画笔去凑；改一处留白要动 XAML 三层。

## 决定

前端换成 **Tauri v2 + Svelte 4 + TypeScript**，新应用在 `src/KeyMouse.EditorUI/`；**WinUI 版保留不动**，
直到新外壳功能对齐后再决定是否退役（一次只让一个版本是"能当真用的"）。

配套的两条硬约定：

1. **界面进程永不提权。** 外壳（Rust）只做三件事：开窗口、调命令行、转发事件。
2. **系统交互只有一条路：`KeyMouse.exe`。** 需要权限的动作（录制、注入、读屏）由命令行/常驻 Runner 去做，
   它的提权由用户或安装程序决定，与界面无关。这条和 README 里"路径只在 `ProductLayout` 判定一次"是同一套思路。

## 为什么不是别的

| 候选 | 否掉的原因 |
| --- | --- |
| 继续 WinUI 3 | 代价不在"写起来别扭"，在上面那三条；换渲染层才有意义 |
| Avalonia | 只解决第 3 条，第 1、2 条原样保留（界面仍要提权、打包仍要自包含发布） |
| Electron | 100 MB 起步、Chromium 沙箱与提权冲突；这个产品的命令行才 0.6 MB |
| Flutter | 钩子/悬浮窗全要 FFI，且多一门 Dart；桌面输入法体验不稳 |

Avalonia 不是坏选择——如果目标只是"摆脱 WindowsAppSDK"。这里的目标还包括"界面不该拿管理员权限"，所以选 Tauri。

## 后果

**得到的**：

- 界面进程是普通权限，出问题也炸不到系统；录制/注入交给已经存在的 `KeyMouse serve`。
- 样式就是 CSS：`Themes/Palette.xaml` 里那套 token 直接变成 CSS 变量，改留白不用动三层模板。
- 包体：外壳 ~10 MB（Rust，含 WebView2 接口）+ 前端 ~100 KB；不需要 WindowsAppSDK 自包含，也不需要 `.pri` 补丁。
- 开发时浏览器 devtools 直接看 DOM/样式，热更新秒级。

**付出的**：

- 多两套工具链（Node + Rust）。`pnpm install` 与 `cargo build` 的首次耗时都要算进新人的上手成本。
- 依赖系统 WebView2 运行时（Win11 自带；Win10 需要装 Evergreen Runtime，安装程序要检查）。
- 界面要重写一遍；检查器那套（现在的 `InspectorBuilder`）在新壳里改成数据驱动的表单。
- 中文输入法在 WebView2 里的行为要实测（组合输入、候选窗位置）——这条在 P1 就验，不留到最后。

## 迁移计划

| 阶段 | 内容 | 判据 |
| --- | --- | --- |
| **P0 外壳跑通** | `src/KeyMouse.EditorUI` 打开能显示同一屏（顶栏/工具栏/两栏/底部输出），状态栏能显示命令行版本；Rust 只做 `runtime_info` | 截图与 WinUI 版并排看不出"这是另一个程序" |
| **P1 契约层** | 把界面要的能力收敛成一份 JSON 面（打开/保存/校验/运行/录制/取区域/读屏），先在 Rust 侧实现，前端只认这份契约；写成文档 | 契约文档 + 前端不直接读文件系统 |
| **P2 功能对齐** | 步骤增删改、检查器（数据驱动）、录制（含悬浮停止）、试运行/播放、输出面板、证据截图 | 冒烟里的编辑器相关检查能在新壳上跑 |
| **P3 打包** | 安装程序带新外壳；WebView2 运行时检测；`pack-release.ps1` 去掉 `.pri`/语言目录补丁 | `verify.ps1` 全绿，安装包体积下降 |
| **P4 收尾** | 决定 WinUI 版退休时机；文档与截图全部换成新壳 | 仓库里只有一份编辑器实现 |

## 未决问题

1. 文件对话框走 `tauri-plugin-dialog` 还是自绘（要统一视觉）。**现状**：官方对话框（P1a 起），视觉是系统的。
2. 证据截图（PNG）怎么显示：`asset` 协议 + 作用域，还是 Rust 读成 base64 给前端。
   **现状**：Rust 读成字节、前端造 `Blob` URL（比 base64 省一次膨胀，也不需要 `asset` 作用域），
   打开大图走 `tauri-plugin-opener`。
3. 单实例与"用命令行打开某个流程"（现在 `KeyMouse flow edit x.json`）：新壳要不要接受同样的参数。
   **现状**：新壳接受 `keymouse-editor.exe 流程.json`（`RuntimeInfo.initialPath`）；单实例做了
   （第二次启动转 `shell://open-external` 给已在跑的进程）。
4. 崩溃日志落点：现在是 `Scratch`（TEMP→LOCALAPPDATA→程序目录），Rust 侧要复用同一套规则。
5. **录制的权限路线**（P2 起）：现在按外壳的权限录，提权窗口上的点击录不到，命令行会在输出里说明，
   要录那些窗口得手敲 `KeyMouse --elevate record`。可选路线：外壳自提权（界面也跟着提权，
   与"界面永不提权"冲突）、录制交给常驻 `KeyMouse serve`（要 UAC 起一次常驻）、或不处理（现状）。
   **现状**：走"按需提权"——「录制」旁边的 `▾` 里有「以管理员权限录制」，
   它跑的是 `KeyMouse --elevate record …`，命令行自己用 `runas` 起管理员进程并等它结束，
   所以外壳照旧不提权、退出码照样拿得到（UAC 被拒 = 1223）。
   代价写进了 `docs/reference/editor-ipc.md` 与使用手册：那一轮**停不了也杀不掉**（UIPI），
   只能用户自己按 `Ctrl+Alt+Q` / 连按两次 `Esc`，因此界面不给它配「停止」，也不进迷你模式。
已解决：

- **录制时够得着停止按钮**（原第 6 条）：试过"另开一块 webview 当悬浮球"，本机内容区始终空白
  （换 `WebviewUrl::App` / `External`、换透明、换创建时机、延迟创建都不行，连
  `data:text/html` 也白），判定为环境级限制而非配置问题；老外壳的球是自绘 Win32 窗口，
  不依赖 webview。**最终做法**：录制时把主窗口本身缩成 420×52 的细带（`enter_mini` /
  `leave_mini`），位置不动、内容换成抓手 + 计时 + 停止按钮。理由：复核过的最短路径是
  "用已经能渲染的那块 webview"，代价是录制期间看不见步骤列表——但那本来也不是录制时要看的东西。

- **关闭窗口时提醒未保存**（原第 5 条）：P1b 已做——外壳拦 `CloseRequested`、发
  `shell://close-requested`、界面答完再 `close_window`。
