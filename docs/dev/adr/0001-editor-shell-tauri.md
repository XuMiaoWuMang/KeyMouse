# ADR 0001：编辑器外壳换成 Tauri，界面进程不再持有管理员权限

- 状态：**已采纳**（P0 外壳已跑通：界面与老版本并排一致、状态行读得到命令行版本、进程不提权；P1 起在推进）
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

1. 文件对话框走 `tauri-plugin-dialog` 还是自绘（要统一视觉）。
2. 证据截图（PNG）怎么显示：`asset` 协议 + 作用域，还是 Rust 读成 base64 给前端。
3. 单实例与"用命令行打开某个流程"（现在 `KeyMouse flow edit x.json`）：新壳要不要接受同样的参数。
4. 崩溃日志落点：现在是 `Scratch`（TEMP→LOCALAPPDATA→程序目录），Rust 侧要复用同一套规则。
- **关闭窗口时提醒未保存**：现在只有「打开/新建」会问，直接点 × 不问。要拦就得在外壳里处理 CloseRequested，再回来问界面（跨边界异步确认），P1b 处理。
