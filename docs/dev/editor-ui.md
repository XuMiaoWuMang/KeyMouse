# 新编辑器外壳（Tauri + Svelte）

`src/KeyMouse.EditorUI/` 是流程编辑器的新外壳（Tauri + Svelte），也是**默认入口**：开始菜单、`KeyMouse flow edit`、
`ProductLayout` 都先找它。老外壳 `src/KeyMouse.FlowEditor`（WinUI 3）仍在仓库、仍随安装包装着，作为退路保留，
直到确认不再需要（计划见 [ADR 0001](adr/0001-editor-shell-tauri.md)）。

功能对照（含每一项怎么验证）写在 [使用指南 → 两个编辑器现在差在哪](../guide/editor.md#两个编辑器现在差在哪)；
接口契约在 [编辑器 IPC](../reference/editor-ipc.md)。

## 无限画布（当前界面）

列表版已经退场：左边那条步骤栏不再存在，整份流程铺在一张可缩放/平移的画布上，右侧只在点节点时滑出抽屉。

- **数据是任意深度的树**：`lib/tree.ts` 用路径表示一个节点——`[{steps,0}]` 是顶层第 1 步，`[{steps,0},{else,0}]` 是它的否则分支第 1 步。读/替换/删除/插入/复制/移动都走路径，所以 `if` 里再套 `while` 也能选中、改字段；`originOf` 与引擎 `6.e1` 同一套写法。
- **视觉**：未选中的轮廓是墨黑（`--kp-ink`），选中/框选命中才换成朱砂；框选矩形内部透明，只留 1px 边。节点形状本身用 `clip-path` / `border-radius` 区分三组，不靠颜色。
- **版式**：`lib/canvas.ts` 只算几何。自动布局是顶层从上到下（一条主线），每个块的子步骤在**右边的一列里往下长**；`if` 的成立时与否则并排，循环只有一条循环体。节点 `208×82`，块下面的主线从整棵子树之后继续，嵌套再深也不会压到后续步骤。自由布局下，`applyNodePositions` 用 `NodePosition` 覆盖手工坐标，并清空自动分支标签/缝隙/旧连线；空分支的「＋」跟着所属块走，画布宽高按所有节点重新算。坐标允许为负：`FlowCanvas` 把逻辑原点放在 `WORLD_ORIGIN=4096`，外层 `.world` 只负责缩放平移，内层 `.world-content` 平移回固定原点，因此左侧/上方不会被裁；`contentBounds` 汇总节点与空槽包围盒，`home()/fit()` 都用它算偏移。节点、分支标签、空分支槽、连线、拖拽缝隙全是数据，`canvas.test.ts` 盯住方位、缝隙归属与坐标覆盖。
- **条件不是手写框**：`ConditionEditor.svelte` 从 `conditions.ts` 的选项生成 `cond`（总是/比较/text/window/front/found/elapsed/自定义）；缺必填项生成空串，`missingOf` 与 `missingHint` 会标「待补 / 还缺 条件」，`text()` 缺 `probe` 时还会多标「条件读哪里」。
- **三种轮廓**：`categoryOf(type)` 分三组——流程控制（六边形：call/if/set/assert/break/continue/return）、循环控制（胶囊：repeat/foreach/while/do/for）、操作指令（纸卡：其余）。`FlowCanvas.svelte` 用 clip-path / border-radius 画轮廓，形状本身就是分类，不再重复写一行类别字。
- **语义连线**：`lib/graph.ts` 的 `buildGraph(steps)` 沿树生成 `next/true/false/loop/back/exit` 边，与节点坐标无关。`if` 两条出口分别到成立/否则首步（空分支直接连汇合点）；循环体末步同时有 `back`（返回循环头）和 `exit`（去下一步），`break` 只连循环后的 `exit`（跳出），`continue` 只连循环头（继续）；`return` 是终端，不画它之后的 `next`。`lib/routing.ts` 只做几何：先算两端中心连线与纵轴的夹角，`<45°` 选上/下边、否则选左/右边；两端各外推 `STUB=18`，候选直角折线优先，被其它节点挡住时用网格 A*（`CLEARANCE=10`、`TURN_COST=24`）绕行；`points` 保留两端引线，输出 `M/L` 直角折线，标签取路径总长中点。`EdgePort {side,t}` 可覆盖任一端，t 限制在 8%–92%，由 `FlowCanvas` 的端点拖拽写入。`graph.test.ts` 与 `routing.test.ts` 是纯函数测试。
- **选中与抽屉**：`selectedPath` 是树路径，不再有"第几组第几个"的两层限制。点节点 → App 用 `stepAt` 取当前步骤 → 右侧抽屉复用 `Inspector.svelte`；点空白/`Esc` 收起。抽屉底部的复制/上移/下移/删除都按同一路径走。
- **多选与长按**：`selectedPaths` 是选择集，`selectedPath` 只是抽屉的主选。`Ctrl/⌘+点`切换、`Shift+点`按文档顺序扩选、`Ctrl+A`全选、**左键拖空白画框**按矩形框选；按住节点 450ms（`LONG_PRESS_MS`）加入选择，之后拖动就是整组搬，轻点仍只开抽屉。`Delete` 删整组；`Ctrl+C` / `Ctrl+V` 复制选中项、按文档顺序粘到主选节点后面（`copyNodes` / `insertManyAt`，父与子同时选中时只复制父）。
- **自由移动**：`FlowCanvas` 的 `freeLayout` 默认开；拖动节点时把被拖根节点及其全部后代放进 `previewPaths`，节点用实时 transform，`graphEdges` 也用预览坐标，所以拖动过程中箭头一直跟着。松手后 `onmovefree` 把每个移动节点的新 `{path,x,y}` 交给 App，写进 `nodePositionByStep`（`WeakMap<FlowStep, NodePosition>`）并持久化到 `localStorage` 的 `keymouse.layout.v1:<文件路径>`；布局不写进流程 JSON。坐标不再 clamp 到 0，允许左侧/上方负坐标。切「结构视图」后位置仍在，切回来继续用；「整理」清空 WeakMap、`edgePorts` 与两条本地存储并重新适配。
- **四向快速添加**：`FlowCanvas` 只对单选节点渲染 `.quick-add`。上/左按钮的回调是 `onquickadd(path,'before')`，下/右是 `'after'`；`App.svelte` 的 `openQuickAdd` 把 `{path,position}` 存进 `quickAdd` 并打开同一个添加面板，`addStep` 优先按这个 target 在**同父同分支**插入兄弟节点（上=前插，下=后插），不会再套用「选中块→加进第一条分支」。面板底部 `quickAddHost` 会写明插入位置；`selectPath`/关闭面板/Esc 都清空 `quickAdd`。
- **连线端点**：`FlowCanvas` 为每条边画一条 `.edge-hit` 透明宽命中路径；单击选中（`activeEdgeKey`），双击调 `onportreset` 恢复自动。选中后两端渲染 `.port-handle`，按下只记录 `portDrag {key,end,rect,port}`，移动和提交统一由画布/窗口的 pointerup 处理（`onViewportMove` / `onWindowPortUp`），避免 pointer capture 在 WebView 里丢失；坐标按 `WORLD_ORIGIN` 转回逻辑坐标，投影到 `nearestPort`。松手调 `onportchange(key,end,port)`；App 写 `edgePorts` 并持久化到 `localStorage` 的 `keymouse.ports.v1:<文件路径>`，不写流程 JSON。
- **拖动排序（结构视图）**：`FlowCanvas` 为每条兄弟序列生成"缝隙"，拖动时所有分支的缝隙都可落，唯一拒绝的是落进被搬节点自己的子树；落点回调 `handleMove` → `moveNodes`。拖到任意节点卡片（含下沿）上会生成一个该节点同层级的虚拟落点，插入到它后面——所以一级/二级/三级之间可以任意互拖，不必去找整棵子树末尾的根缝隙。`Ctrl/⌘+点`把节点加进选择集，拖动其中任意一个会整组搬；`moveNodes` 按文档顺序删除再插入，并逐层重算因删兄弟而前移的目标父路径。空分支仍由虚线槽上的「＋」创建第一步。
- **缩放/平移**：滚轮以鼠标为中心缩放（0.25–1.8），**右键拖空白平移**；`home()` 首屏按宽度给出可读比例（不为了塞下全部缩到 30%），左下角「适配」才把整张图缩到一屏。
- **测试**：`tree.test.ts`（路径/增删改/嵌套 origin）、`canvas.test.ts`（方位/分支/缝隙/手工坐标）、`graph.test.ts`（顺序、if 双出口、循环回边/退出、break/continue、return 终端、空分支/空循环）、`routing.test.ts`（角度选边、45° 边界、固定/自动接口、障碍绕行、引线、标签，共 16 条）是纯函数测试；前端总数 185，命令 `km ut`。

## 为什么另起一个目录

- 老外壳的界面进程要管理员权限（为了录得到提权窗口的点击）。新外壳**界面永不提权**：需要权限的动作
  交给 `KeyMouse.exe`，需要时按次弹 UAC（`录制 ▸ → 以管理员权限录制`）。
- 老外壳的打包被 WindowsAppSDK 拖着走（`.pri`、96 个语言目录、bootstrapper）。新外壳只有 WebView2。
- 两套并存是刻意的：删早了会丢掉一条退路。

## 目录

```
src/KeyMouse.EditorUI/
  index.html            挂载点，只引 /src/main.ts
  package.json          pnpm 脚本：dev / build / check / test:unit / tauri
  vite.config.ts        开发服务器固定 5183；Svelte 的 TS 预处理在这里开；忽略 src-tauri 的监听
  tsconfig.json         独立配置，不继承外部预设
  src/
    main.ts             挂 App 到 #app，引入主题
    App.svelte          整屏界面：顶栏 / 工具栏 / 画布 / 抽屉 / 底部输出 / 卡片
    lib/theme.css       主题 var：纸、墨、朱砂、分隔线、字阶、圆角
    lib/model.ts        流程文档类型、步骤标题与摘要、三种分类与分支
    lib/tree.ts         任意深度的路径读写（选中 / 替换 / 删除 / 插入 / 复制 / 移动 / origin）
    lib/canvas.ts       画布几何：节点、分支标签、空槽、连线、拖拽缝隙、手工坐标覆盖
    lib/graph.ts        流程树 → 语义连线（next/true/false/loop/back/exit），含 break/continue/return
    lib/routing.ts      两个矩形 + 端口 → 直角折线路由、避障与标签位置
    lib/FlowCanvas.svelte 无限画布：自由摆位 / 结构排序 / 缩放平移 / 多选 / 实时连线
    lib/schema.ts       解析 `KeyMouse schema` 的契约 JSON，供抽屉按 kind 渲染
    lib/steps.ts        二十三种步骤模板、三组分类、必填项
    lib/edit.ts         顶层增删改排序（老纯函数，仍被部分逻辑复用）
    lib/fieldpath.ts    点号路径读写（空值删键、删空嵌套连父键）
    lib/run.ts          退出码结论、节点结果（按报告 origin 落节点）
    lib/progress.ts     从 `[ 3/8 ]` 里读"跑到第几步"
    lib/StepIcon.svelte 一套自绘步骤图标
    lib/Inspector.svelte 按契约渲染的参数抽屉内容
    lib/InspectorShape.svelte 嵌套形状（target/region/probe/when）的字段排版
    lib/KeyValueEditor.svelte 名字 → 表达式的小表（for 的 init/step、call.vars）
    lib/conditions.ts 条件选项 ↔ cond 表达式的解析与生成（含必填判定）
    lib/ConditionEditor.svelte 条件类型下拉与必填输入（text()/window()/found()/elapsed()…）
  src-tauri/
    Cargo.toml          tauri / serde / 插件：dialog、opener、single-instance
    tauri.conf.json     窗口尺寸、devUrl 5183、bundle 关（安装程序自己打）
    capabilities/       主窗口能做什么（对话框、打开文件、改标题……）
    src/main.rs         开窗口、注册命令、单实例、崩溃日志、窗口尺寸恢复
    src/layout.rs       找仓库、找命令行、报环境
    src/files.rs        打开/保存流程、写临时副本、读证据图
    src/schema.rs       跑 `KeyMouse schema` 并缓存
    src/pick.rs         屏幕取点/取块
    src/run.rs          试运行/播放/停止/暂停/读报告
    src/record.rs       录制（当前权限与提权两条路）
    src/hud.rs          左上角运行日志 HUD（透明、置顶、鼠标穿透）
    src/hotkey.rs       播放的全局停止键 Ctrl+Alt+Q
    src/overlay_style.rs 透明置顶窗口的 Win32 样式位
    src/mini.rs         旧版缩窗实现（保留以免老命令失效）
    src/store.rs        最近打开与窗口位置/尺寸（%LOCALAPPDATA%）
    src/process.rs      起命令行时隐藏控制台窗口
    src/crashlog.rs     panic 与界面异常落 editor-crash.log
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

## 现在是什么样

- **一张无限画布**：顶栏（标记 + 文件名 + 文件动作）、工具栏（＋添加 / 自由布局·结构视图·整理 / 节点数 / 录制·试运行·播放····）、画布、底部输出条。没有左栏列表；节点详情从右侧抽屉进出。
- **三组操作**：添加面板按流程控制 / 循环控制 / 操作指令排成三列；三种轮廓分别是六边形 / 胶囊 / 纸卡。`makeStep()` 只放"这一步是什么"，参数留空，节点标「待补」。
- **任意深度**：`if` 里套 `while` 的每一层都能选中、改字段、增删和摆放；路径在 `lib/tree.ts`，origin 与引擎的 `6.e1` 对齐。自由布局的坐标按路径/步骤对象保存，结构排序仍由 `moveNodes` 改树，二者互不覆盖。
- **画布手势**：滚轮缩放；**左键拖空白框选、右键拖空白平移**；自由布局下拖节点只改位置（块连同后代一起走），切到结构视图后拖节点才是排序（可跨分支/换层）、`Ctrl/⌘+点`多选、`Shift+点`范围、长按节点加入选择、`Ctrl+A`全选、点节点开抽屉；左下角有 `− / 百分比 / ＋ / 适配`。首屏按宽度给出可读比例，`适配` 才把整图缩到一屏。
- **打开/保存/录制/播放/撤销**：文件对话框走 Tauri dialog；播放与录制默认隐藏主窗口，左上角留一块只读日志 HUD；停止统一按 `Ctrl+Alt+Q`，结束后主窗口自己回来；试运行不隐藏。
- **输出与结论**：退出码翻译在 `lib/run.ts`，节点结果按 `origin` 落位；普通提示只更新底部一行，失败才展开输出面板。输出里选中的文字可以点「复制选中」。
- **测试**：`tree` / `canvas` / `edit` / `filter` / `run` / `fieldpath` / `hud` 等纯函数单测共 195 条（`km ut`），另有 `svelte-check` 与 cargo 测试（36 条）。


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
  [外壳契约](../reference/editor-ipc.md)）。位置：右侧抽屉最下方「证据」一节（缩略图 + 文件名 +
  「看大图」「在文件夹里看」），画布节点右上角有一个 ▣ 记号。
  为什么缩略图只有 140px 高：再高就会把抽屉其它字段顶到滚动外面，而它们是改这一步时要同时看见的。
  要不要点开由用户决定（点击交给系统看图程序，不是自己再写一个预览窗）。
- **录制/播放时看得见进度、停得下来**：两者都先隐藏主窗口，左上角亮一块只读 HUD
  （`hud.rs` + `hud.ts`，最近 7 步、没有按钮、鼠标穿透）；停止统一按 `Ctrl+Alt+Q`。
  细节见本文末尾「运行日志 HUD」。
- 还没做的（写在 ADR 的未决问题里）：行内"跑到第几步"高亮。


## 检查器由契约画出来（P5）

老外壳的检查器是"读 `flow.schema.json` 再按 kind 选控件"，新外壳照做，字段一个都不写死：

- `src/lib/schema.ts` 解析 `KeyMouse schema` 的输出（`FieldContract` / `StepContract` / `ShapeContract`）。
  形状是 `{ comment, fields }` 而不是数组——第一版按数组写，运行时炸在
  `shapeFields(...).every is not a function`，这条教训留在函数注释里。
- `src/lib/Inspector.svelte` 按 kind 渲染：`text`/`multiline`/`int`/`bool`/`enum`/`target`/
  `point`/`region`/`when`/`probe`/`vars`；`showWhen`（`button` / `!button`）决定二选一步骤里哪个字段出现。
  嵌套形状交给 `InspectorShape.svelte`（同一套 slot 用于 target/region/probe/when），
  `vars` 交给 `KeyValueEditor.svelte`（`for` 的初值/每轮、`call.vars` 都是同一张表）。
  契约把 `note`/`when`/`shot` 放在 `common` 里，所以每种步骤都把 common 拼进来——`shot` 例外，
  证据图有自己的块。块步骤的契约标了 `noWhen`，检查器不画那个开关：条件已经写在 `cond` 里。
- **窗口位置也记**：`store::remember_size` 关窗时一起存 `outer_position()`（物理像素），下次在 `.setup()`
  里用 `PhysicalPosition` 原样塞回。两者必须成对——一端按逻辑、一端按物理，窗口每开一次就漂一段
  （实测过：`outer_position` 与 `set_position(Physical)` 配对时，连开两次的坐标完全一致）。
  位置单独判空：老账本没有 x/y，缺省 0/0 会把窗口甩到左上角。
- **体检与日志也是"搬话"**：`run_doctor` 跑一次命令行的 `doctor` 把整段输出带回来（环境判断只在
  命令行里有一份，界面复制那套迟早会分叉）；`crash_log_path` 让「看崩溃日志」能直接打开
  `editor-crash.log`。两条都点在 `···` 菜单里。
- **`···` 菜单的四件事之外还多两项**：「在文件夹里看这个流程」（走 `revealItemInDir`）与
  「键盘与版本…」——后者是一张就地卡片（键位表 + 当前命令行路径 / 版本 / 环境），
  不打开文档也能回答"我现在连的是哪一个 KeyMouse"。`Esc` 会先收这张卡片。
- **块节点也有结果**：命令行给内联子流程/循环的位置是 `2.1`、`2.2`，块自己没有命令。
  `resultsFromReport` 会往上卷一层——父行没有自己的结果时，用孩子们的结果汇总成一句
  （「2 步都成功」/第一个失败的原因）。否则「调用另一个流程」那行永远是空的，明明它跑过了。
- **组里的步骤也是一等公民**：`childSel` 是"这个块所有分支摊平后的第几个"（`childrenOf`），
  删除 / 上下挪 / 改字段都先换成 `{branch, index}` 再交给 `removeChildAt` / `moveChild`（纯函数 + 单测）；
  组里删到空时连 `steps`/`else` 字段一起收掉，免得 JSON 里留下一个空数组、读起来像"被清空过"。
  ↑/↓ 按画布阅读顺序走节点；Alt+↑/↓ 只在同一分支里换位。
- **播放前先查必填项**：`firstIncomplete()` 用 `lib/steps.ts` 的 `missingOf`（与检查器右上角那句同源）
  找第一条缺项的步骤，缺就选中它并在底部说清楚，不放行——否则命令行会在中途报同样的错，
  而且是在已经发出一半输入之后。试运行不拦：它就是拿来看错的。
  `key` 只有一个 `combo` 必填字段：单键写 `enter`，组合键写 `ctrl+shift+s`；
  旧文件的 `text` 在打开时由 `normalizeKeySteps` 搬进 `combo`，保存后旧字段消失。
- **窗口标题跟着文件和脏标记走**：`syncTitle()` 把标题写成「文件名 · KeyMouse 流程编辑器」，
  未保存时前面加 `•`。任务栏上光看「流程编辑器」分不出开的是哪一份。`setTitle` 要
  `core:window:allow-set-title` 能力（`src-tauri/capabilities/default.json`），没有它调用会
  被 ACL 静默拒掉——界面里 `.catch` 吞掉错误，看起来就是"标题不变"。
- **脏标记不是"改过"而是"和上次存盘不一样"**：`savedJson` 记住载入/保存时的序列化快照，
  撤销回到那一刻自动清掉 `•`，关窗也不再问"要保存吗"。
- **行上结果**：命令行带 `--report` 跑，报告里每步都有 `origin`（`3` 表示第三个顶层步、
  `6.1` 表示第六步的第一个子步、`6.e1` 表示否则分支的第一个子步），`src/lib/run.ts` 的 `resultsFromReport` 把它读成
  「行位置 → 结果」的字典，`App.svelte` 用 `originOf(index, childIndex)` 取同一个键。
  **分组步不再需要换算**：循环展开三次时 `origin` 三次都是 `6.1`，最后一次执行留在那一行上——
  这正是"这一行长什么样"的语义。子步骤行也有结果位，收起时看不到、展开就能看。
  跳过（`skipped`）的步骤写「跳过：<命令>」并按未成功着色，免得把"没跑"看成"成功"。
- `src-tauri/src/store.rs`：界面自己的账本（最近打开 8 条、上次窗口尺寸），落在
  `%LOCALAPPDATA%\KeyMouse\editor-settings.json`。读写都吞错——账本坏了就当没有。
  窗口尺寸在 `.setup()` 里恢复（不是界面调 API）：那时窗口还没显示，不会先闪一下默认大小。
- `src/lib/fieldpath.ts` 负责按 `when.timeoutMs` 这种点号路径读写：写空值等于删键，删空的
  嵌套对象连父键一起去掉，返回新对象（撤销栈是序列化快照，原地改会让快照跟着变）。
- 老的"待补"判定（`lib/steps.ts`）还在，用于快速加步骤时给个提醒；抽屉里的必填标来自契约的
  `required`。
- 取点/取块走 `pick_region`（`src-tauri/src/pick.rs`）：点按钮先把主窗口 `hide()`，浮层选完/取消后 `finally` 里 `show()` + 聚焦；隐藏失败不让取点流程卡住。客户区/窗口坐标按 `shouldAttachWindow()` 自动填 `target.process` 与标题，屏幕坐标不绑定窗口。`region pick --json` 必须带 `window.process`（CLI 的 `RegionCommand.WriteJson` 负责；只有 pid 时前端补不出进程名）。外壳只把结果写成坐标 + 顺手填
  `target.process`，浮层与坐标换算仍在命令行的 `region pick` 里。

## 控制流块行（2.4.0）

引擎支持 C 风格控制流之后，编辑器必须能"看着改"，而不是只把 JSON 渲染成只读：

- **分支是模型的一部分**：`model.ts` 的 `branchesOf(step)` 给 `if` 返回「成立时 / 否则」两条，
  给 `while/do/for/repeat/foreach` 返回一条；`childSteps` 是两条合起来（计数/过滤用），
  `childSel` 是它们摊平后的下标。否则分支还没写时也返回空分支，界面上才能显示「否则」并往里加第一步。
- **`else` 的 origin 用 `.e`**：否则分支第一行是 `6.e1`，与引擎 `FlowPlan` 生成的 origin 对齐；
  行号也这么显示。不能写成 `6.2`——那会和「成立时」的第二个子步骤撞键，运行结果会落错行。
- **新建模板自带最小身体**：`if`/`while`/`do` 插进来就带一个 `sleep`，`for` 带 `i=0; i<3; i=i+1`，
  所以画布上立刻能展开、往里加步骤；条件空着，节点上照旧显示「待补」。
- **条件输入是等宽的一行**（`cond` / `set.value`），`probe` 只在条件里出现 `text(` 或已经填过时展开，
  `while (i < 3)` 不会逼人填窗口。`probe` 的字段（target/region/match/maxErrors）由 `InspectorShape` 排版。
- **`for` 的三段式**：`init`/`step` 都是 `KeyValueEditor`，名字 → 表达式；`cond` 是单行表达式；
  `maxRounds` 是数字框。契约把初值/每轮的说明放在 help 里，标签只留短名，窄抽屉不会折成两行。
- **`noWhen` 来自契约**：`repeat/foreach/if/while/do/for/break/continue/return` 的契约条目标了 `noWhen`，
  检查器不画「前提条件」；`set`/`assert` 仍然可以带单步前提。
- **必填提示会走到两条分支**：`firstIncomplete()` 遍历 `childrenOf`，找到哪条分支的哪一步就选中哪一步
  （`childSel` + 展开），而不是只报顶层。
- **测试**：`steps.test.ts` 盯模板/分支/摘要；`edit.test.ts` 增加「否则分支增删改」；
  `filter.test.ts` 确认否则里的步骤也能搜到；`pnpm check` 与 185 条前端单测（`km ut`）一起跑。

## 排版语汇：归藏 Page 的杂志风

界面的字号、颜色、线宽不是随手调的，取自 `guizang-page-skill`（归藏 Page）的**杂志风**：
米白纸、墨色字、等宽元信息、衬线大标题、发丝线、方框标签、章节花饰编号（`✦✦✦ 01 ✦✦✦`）。
落点是 `src/lib/theme.css` 里那套 `--kp-*` token。

- **accent 不跟着文档走**：skill 的 `ink-classic` 强调色近黑，这里保留产品自己的朱砂 `#c2410c`
  ——它是"当前与可动"的唯一颜色，属于产品识别，不属于排版主题。
- **三套主题只换纸与墨**：`paper`（默认）、`kraft`（牛皮纸）、`night`（午夜墨）。
  深色主题下朱砂压在近黑纸上看不清，accent 换暖金 `#e0ab56`（`[data-theme='night']` 里改）。
- **中文不斜体**：浏览器只有伪斜体，斜体只留给西文。
- **线只有一种**：1px `var(--kp-rule)`；2px 只留给设置页报头那条 chrome rule。
- **字体是系统栈**：`Source Serif 4 / Noto Serif SC / Songti SC`，装了什么用什么。
  生产环境不联网取 Google Fonts，所以没打包字体；中文标题实际落在宋体上，这是刻意的取舍。

主题落在账本里（`editor-settings.json` 的 `theme`），Rust 侧 `store.rs` 的 `THEMES`
是白名单：认不出的 id 一律回到 `paper`，免得一个坏值把界面搞成无主题。

### 空状态：一页文档的开篇

"还没有打开流程"那一屏不套卡片边框（旧版是一个大圆角框），改成 document opening：
等宽 kicker（`KEYMOUSE · 流程编辑器`）→ 26px 衬线标题 → 导语（36em、行高 1.85）→
三个动作 → 发丝线 → 等宽「最近」小标 + 带发丝线的目录列表（文件名衬线、路径等宽）。

理由：这一屏是产品给人的**第一眼**，而第一眼最像"文档封面"或"表单"，决定后面整页的语气。
卡片 + 边框会立刻把它拉回"后台管理界面"。
### 流程图那一屏：报头 + 目录式列表

流程图界面不是"一个应用外壳 + 控件"，它借的是**文档的骨架**：

- **顶栏 = 报头**：左边是等宽小字 kicker（`KEYMOUSE · 流程编辑器`），中间是**衬线**的文件名
  （17px / 500 / 字距 +0.02em），下面一行等宽路径，右边是动作。窗口里只有一条 1.5px 实线
  （报头下沿），其余一律 1px 发丝线——这是 skill 的"全文最多两条粗线"规则。
- **未保存**是方框标签（1px 朱砂边 + 等宽小字），不是胶囊：胶囊在纸感排版里像贴纸。
- **工具栏 = 方框标签**：步骤按钮 1px 线 + 透明底，hover 只加深线不铺底色。
  "底"留给选中，"线"留给可以点——两者分开，扫一眼就知道哪个是当前行。
- **列表 = 目录**：编号用等宽 + `tabular-nums`（像页码），步骤名用衬线 15px，
  摘要回到无衬线 12.5px 墨色二档。一列读下来是"章节目录"，不是"一排按钮"。
- **检查器段标题**同样换成衬线 16px，与列表标题同一套字。

最小窗口是 892×632（逻辑像素）：这一套字号与 30px 的按钮高度在这个尺寸下仍然一行放得下
工具栏，列表和检查器也都不需要横向滚动。改字号之前先在那个尺寸里看一眼。
## 设置页（`src/Settings.svelte`）

设置是**整页替换**，不是弹窗：它内容多（外观/文件/环境/快捷键），弹窗装不下，
而且文档式的长页正好是杂志风擅长的事。

- 入口三处：顶栏齿轮、`···` 菜单「设置…」、`Ctrl+,`；返回：顶栏「← 返回流程」或 `Esc`。
- 每一行都真的生效：主题 → `save_theme`；
  最近文件 → `recent_files` / `forget_recent` / `clear_recent`；体检 → `run_doctor`；
  诊断 → `collect_diagnostics`。
- **设置页不自己判断环境**：判断只活在命令行的 `doctor` 里，这里只把它的输出搬回来贴成
  `<pre>`。同一套判断写两份，迟早两份都不一样。
- **打开流程只有一处实现**：设置页点"最近"里的一条时发 `keymouse:open-path` 事件，
  主界面监听后 `loadFrom()`；别在设置页里再写一遍打开逻辑。
- 图标全是内联 SVG（`src/lib/Icon.svelte`）：远程图标一断网就是空白方块，而外壳只依赖
  系统 WebView2；内联 SVG 还能跟 `currentColor` 一起换主题。

## 弹窗一律"点外面就收起"

菜单（`添加`、录制 `▾`、`···`、抽屉）原本只有 `Esc` 和"点自己那一项"能关。拿着鼠标的人
会去点别处，然后菜单挂在那儿压着列表。现在统一在 `<svelte:window on:pointerdown={onWindowPointerDown}>`
里收：`closePopups(except)` 关掉除 `except` 之外的弹窗，`except` 取
`event.target.closest('[data-popup]')?.dataset.popup`。

- **触发按钮和菜单本身标同一个 `data-popup` 值**（`more` / `record` / `ops` / `row`）。点了触发按钮不算
  "外面"，交给按钮自己的 toggle 处理——否则 pointerdown 先关、click 再开，看起来"点了没反应"。
- 右键菜单没有触发按钮，任何 pointerdown 只要不在它自己身上就关。
- 判断用 `closest` 而不是坐标：菜单是浮层，它的 `getBoundingClientRect()` 里 `pointer-events` 之外的
  空白处也算"在里面"，按坐标算会误判。

## 界面与老外壳的关系

P0 的目标是"两屏并排看不出这是两个程序"：

- 顶栏：`▞` 标记 + `KeyMouse` + `流程编辑器` + 当前文件路径（居中，省略号在左侧）。
- 工具栏：`＋ 添加`（三组面板）+ 节点数；右侧 `录制`（圆点）/`试运行`/`播放`（朱砂实心）/`···`。
- 画布：顶层从上到下、块内子步骤在右侧向下；三种轮廓（六边形/胶囊/纸卡）；节点带 origin、摘要、待补、计数与运行结果。
- 抽屉：点节点从右侧滑出，契约字段 / 证据 / JSON / 复制·上下移·删除；点空白或 `Esc` 收起。
- 底部：状态行 + 输出胶囊；点开是等宽字体的运行输出。

配色与间距取的是 `src/KeyMouse.FlowEditor/Themes/Palette.xaml` 的同一套 token，`theme.css` 里以 `--kp-*` 变量落地。

## 起命令行的方式：`process.rs`

外壳起的每一个命令行附属进程都走 `src-tauri/src/process.rs` 的 `cli_command()`——它给
`Command` 加 `CREATE_NO_WINDOW`。理由很朴素：`KeyMouse.exe` 是控制台程序，界面打开文件、
取点、跑流程、读契约都要起它，不隐藏窗口就会每次闪一个黑框。

新增调用点时别直接写 `Command::new(cli)`，从 `cli_command` 走。

外壳自己也不能是控制台程序：`main.rs` 第一行是
`#![cfg_attr(not(debug_assertions), windows_subsystem = "windows")]`——去掉它，release 版每次
启动都会先弹一个黑框（class 为 `ConsoleWindowClass`，标题就是 exe 路径）。

窗口的置顶只有录制/播放 HUD 会临时打开（`hud.rs` 用 `always_on_top` + `overlay_style`）；`main.rs` 的
`.setup()` 里还会显式 `set_always_on_top(false)` 一次，避免上一轮被强杀留下的置顶被带进来。

## 诊断打包：`diagnostics.rs`

用户报"它不动了"时，最花时间的是问清环境。`Ctrl+Shift+D`（菜单里也有）会跑
`collect_diagnostics`，在 `%LOCALAPPDATA%\KeyMouse\diagnostics\<UTC 时间戳>\` 写五份文件：
`环境.txt`（`runtime_info` 的字段，`None` 写成"没找到"）、`体检.txt`（`run_doctor` 原样）、
`崩溃日志尾部.txt`（末尾 400 行）、`设置.json`（`store.rs` 的账本）、`说明.txt`（清单 + 隐私说明）。

三条刻意的取舍写在文件头：单份失败只降级（名字后加"写不进去：…"，其余照写）、崩溃日志只取尾部、
目录名带时间戳所以连打两次不覆盖。时间戳是手写的 UTC（`civil_from_days`），不为一个文件名引依赖。

## 输出面板：不摆环境、粘底、只有一块在滚

三条约束，都是踩过之后才定下来的：

1. **环境信息不进面板**。命令行、版本、提权、环境名这四行以前一直贴在输出上方，占掉 60 多像素；
   而它们只在"出事"时才有用（而且设置页里本来就有）。现在改成派生的 `trouble`：
   输出里出现 `err` 流、或者 `runtime.elevated` / 没有 `cliPath` 时才在底部显示一条窄带，
   带一个直接能点的「打包诊断信息」。
2. **粘底但不抢滚轮**。`outputStick` 记住"用户是不是在底部"，`onOutputScroll` 每滚一次更新它
   （距底 < 24px 就算在底部）；新输出到来时用 `tick()` 等 DOM 更新完再 `scrollTop = scrollHeight`。
   往上翻旧日志时**不**强制拉到最底——否则用户永远读不完一行。
3. **只有 `<pre>` 滚**。面板本身 `display:flex; flex-direction:column; overflow:hidden`，
   `<pre class="run">` 是 `flex:1 1 auto; min-height:0; overflow:auto`，错误条是 `flex:none`。
   以前面板与 `pre` 都在滚，结果是"最新的输出"和"错误条"互相把对方挤出可视区。

改这个面板时很容易把模板的大括号弄丢（我用按行号改模板的方式连着破过三次：
`</section>`、根 `<div class="shell">` 的 `</div>`、`{#if mini}` 的 `{/if}`）。
`pnpm run check` 会依次报 `Unexpected block closing tag` → `<div> was left open` → `Block was left open`，
照着提示逐层补回来即可；改完务必再跑一次，别只看第一行。

## 退出码文案：照抄 `cli.md`，还要带上"那一步自己说了什么"

`src/lib/run.ts` 的 `CODE_HINTS` 只放 `docs/reference/cli.md` 那张表里的含义。**别凭印象编**：
曾经把 `4` 写成"编辑器路径不对"，用户看到的正是「没跑通（退出码 4）：编辑器路径不对。」，
而真相是「目标窗口不可用：HYP(27036) 米哈游启动器 已最小化」。界面上报原因只有两条路——
照抄文档，或者只报数字。

光有"退出码 4 是什么"还不够，用户要的是"为什么"。所以两件事一起做：

1. `resultsFromReport` 在失败行上用报告里的 `output` 第一行（`firstLine`：剥 `错误：`、压成一行、
   超过 72 字截断），成功行仍用命令行文本——一行要么说"做成了什么"，要么说"卡在哪儿"。
2. 结论里拼上第一步失败的说法：`runConclusion(code, cancelled, dryRun, reason)` 的第二段，
   `reason` 从 `firstFailure(results)` 来（按点分路径挑最小行号，去掉 `退出码 N：` 前缀）。

两条路都要读报告：**播放**走 `run-exit` 事件，**试运行**走 `validate_flow` 的返回值——两者都写
`--report`，所以两处都得先 `read_run_report` 再下结论。只改一条的话，另一半还是"退出码 X"没有下文。
注意 `read_run_report()` 读不到文件时返回空串、**不会 reject**，`.catch()` 那条兜底其实摸不到。
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
## 没保存也能跑，起始页也能加步骤

两条都来自同一个约束：命令行只认文件。

- **加步骤不再要求先有文档**：工具栏按钮原来带 `disabled={!doc}`，`addStep` 里也有
  `if (!doc) return`，于是在起始页点「点击」什么都不发生。现在没有文档就先建一份空的
  （`newFlow()`），再按工具栏顺序插入——起始页不是"死的介绍页"。
- **试运行/播放不再逼人先存盘**：没有路径或改过没保存时，先把当前内容写进
  `%LOCALAPPDATA%\KeyMouse\tmp\editor-run.json`（外壳命令 `write_scratch_flow`），再拿这个
  路径去跑。磁盘上正在编辑的文件一个字都不动，底部输出会写明"跑的是临时副本，磁盘上的
  文件没动"。已保存且无改动时仍直接跑原文件。

## 录制 / 播放日志 HUD：一块只读的透明第二窗口

录制和播放都会先隐藏主窗口，左上角那块深色小面板是**独立的第二个 webview**
（窗口 label `hud`，入口 `hud.html` + `src/hud.ts`）。它没有按钮，也不接收点击，
只显示最近 7 步在做什么。旧版那条带停止/计时器的浮动药丸已经删除。

**位置与尺寸**：`src-tauri/src/hud.rs` 的 `hud_position(origin, size, scale)` 把窗口钉在当前显示器
左上角，`LEFT_MARGIN = TOP_MARGIN = 16` 逻辑像素，尺寸 `468 × 210`；多显示器用
`current_monitor()` 的 origin/scale，副屏在主屏左侧也能落对。

**只读、不抢焦点**：`overlay_style.rs` 给窗口加
`WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_TRANSPARENT`，并 `set_ignore_cursor_events(true)`；
`show()` 用 `focused(false)`，不进 Alt+Tab / 任务栏。`hide()` 用 `close` 而不是 `hide`：
透明无边框窗口 hide 后再次 show，第一帧常常是未重绘的黑块。

**不被 OCR / 截图看见**：`overlay_style.rs` 还给窗口调了
`SetWindowDisplayAffinity(hwnd, WDA_EXCLUDEFROMCAPTURE)`。HUD 对用户仍然可见，但本项目的
屏幕抓取（`BitBlt` + `CAPTUREBLT`）和系统截图 API 都拿不到它——`probe` 抓到的是 HUD 下面的
窗口内容，不是日志文字。该标记从 Windows 10 2004 起可用；更旧的系统会返回失败，此时 HUD
仍可能进入截图。

**为什么轮询而不是事件**：HUD 是第二个 WebView，页面加载时间不由命令行决定；第一条
`执行中` 往往在它挂载前就发出了。`run.rs` 把每一行同时推进 `HudLog`
（`Mutex<VecDeque<String>>`，容量 240），`hud.ts` 每 250ms 调 `invoke('hud_lines')`
拿完整快照再比较重绘。晚挂载也能立刻看到当前进度，长 `sleep` 期间不会只剩一句
“等待第一步…”。

**当前步骤行**：`FlowRunner` 在 `Execution.StepStarted` 后，若 `KEYMOUSE_HUD=1` 且不是动态步骤，
打印 `  [{stepNo,3}/{PlannedSteps}] 执行中  {Describe(step)}`；`run.rs` 给编辑器起的子进程注入
这个环境变量。主输出面板会过滤掉这类开始行，HUD 则把开始行和完成行按步骤号合并。

**停止**：`src-tauri/src/hotkey.rs` 在 `run_flow` 起子进程前注册
`Ctrl+Alt+Q`（`RegisterHotKey`，独立消息线程），收到 `WM_HOTKEY` 调 `run_cancel`；
子进程退出时等待线程注销热键。注册失败就不启动流程，避免一段没人能叫停的播放。
录制仍走命令行自己的全局停止键与命名事件。

**capabilities**：`src-tauri/capabilities/hud.json` 只给 `core:default`，窗口不能开文件、
不能发别的命令，也收不到点击。
