# KeyMouse 命令参考

完整的命令、选项与退出码。概览见 [README](../README.md)，设计取舍见 [design.md](design.md)。

## 调用形式

```
KeyMouse <group> <command> [参数] [选项]
```

六组命令：`mouse`、`key`（别名 `keyboard`）、`window`、`run`、`probe`（只读）、`region`（只报坐标）。
无参数运行会打印内置帮助；`KeyMouse help probe` 打印某一组的完整选项。

---

## mouse

| 命令 | 说明 |
| --- | --- |
| `mouse pos` | 打印当前光标坐标 |
| `mouse move <x> <y>` | 移动到绝对屏幕像素 |
| `mouse move -wx <cx> -wy <cy> <选择器>` | 移动到目标窗口客户区的某点 |
| `mouse moveby <dx> <dy>` | 相对移动 |
| `mouse click [button] [-x X -y Y] [-n N] [-i MS]` | 点击；`-n` 连击次数（默认 1），`-i` 间隔（默认 80ms） |
| `mouse dblclick [button] [-x X -y Y]` | 双击（等价于 `click -n 2`） |
| `mouse down [button]` / `mouse up [button]` | 按住 / 松开（可做长按） |
| `mouse wheel <delta> [-x X -y Y]` | 滚轮；`120` = 一格，向上为正 |
| `mouse hwheel <delta>` | 横向滚轮 |
| `mouse drag <x1> <y1> <x2> <y2> [--button B] [--steps N] [--duration MS]` | 拖拽（插值移动，默认 20 步 / 400ms） |
| `mouse drag -wx <cx1> -wy <cy1> --wx2 <cx2> --wy2 <cy2> <选择器> [--button B] [--steps N] [--duration MS]` | 拖拽两点之间（**客户区**坐标） |

`button`：`left`（默认）、`right`、`middle`、`x1`、`x2`

带选择器时，任何鼠标命令都会**先聚焦目标**（见下面的窗口策略）；`mouse pos` 不接受选择器。

---

## key

| 命令 | 说明 |
| --- | --- |
| `key press <key> [-n COUNT] [-i MS]` | 敲击，可连按（默认间隔 60ms） |
| `key down <key>` / `key up <key>` | 按下 / 抬起 |
| `key combo <k1+k2+...> [--hold MS]` | 组合键，如 `ctrl+shift+s`、`win+r`、`alt+f4`；修饰键先按、反序释放 |
| `key type <text> [--interval MS]` | 输入文本；Unicode 注入，**不依赖输入法/键盘布局**，中文直接打；默认 15ms/字，`--interval 0` 最快但可能丢字 |

### 按键名

| 类别 | 名称 |
| --- | --- |
| 字母 / 数字 | `a`-`z`、`0`-`9` |
| 功能键 | `f1`-`f24` |
| 编辑 | `esc`(`escape`)、`enter`(`return`)、`tab`、`space`(`spacebar`)、`backspace`(`bksp`)、`delete`(`del`)、`insert`(`ins`) |
| 导航 | `home`、`end`、`pageup`(`pgup`)、`pagedown`(`pgdn`)、`up`、`down`、`left`、`right` |
| 修饰 | `ctrl`(`control`)、`shift`、`alt`、`win`(`lwin`)、`rwin`、`apps`(`menu`)、`capslock`(`caps`) |
| 小键盘 | `num0`-`num9`、`numadd`、`numsub`、`nummul`、`numdiv`、`numdecimal`、`numenter`、`numlock` |
| 其它 | `printscreen`(`prtsc`)、`pause`、`scrolllock` |
| 符号 | `semicolon`、`equals`、`comma`、`minus`、`period`、`slash`、`grave`(`backtick`)、`lbracket`、`rbracket`、`backslash`、`quote` |
| 逃生口 | `vk:0x5B` —— 直接用虚拟键码，`0x` 可省略 |

大小写不敏感。方向键、`insert`/`delete`/`home`/`end`/`pageup`/`pagedown` 会自动带上扩展键标志。

---

## window

| 命令 | 说明 |
| --- | --- |
| `window list [--filter <串>] [--process <名>] [--all]` | 列出顶层窗口及其状态；默认只列**可见且有标题**的，`--all` 连隐藏/无标题的一起列 |
| `window inspect <选择器>` | 逐条打印资格判定：每个匹配窗口为什么可用 / 不可用 |
| `window focus <选择器>` | 只做聚焦 + 验证，不发送任何输入 |

`window list` 的`状态`列会标出：`可见` / `隐藏`、`最小化`、`已遮盖`、`已禁用`、
`属主=0x...`（该窗口属于某个主窗口，即次要窗口）、`响应=Nms` / `无响应` / `未探测`。

---

## 窗口选择器

所有鼠标/键盘/窗口命令都接受；多个条件之间是 **AND**。

| 选项 | 说明 |
| --- | --- |
| `--title <子串>` | 标题子串，大小写不敏感 |
| `--title-exact <文本>` | 标题完全匹配 |
| `--class <类名>` | 窗口类名（完全匹配） |
| `--process <exe 名>` | 进程名，如 `notepad`、`QQ` |
| `--pid <n>` | 进程 ID |
| `--hwnd <0x1234\|1234>` | 窗口句柄（十六进制或十进制） |
| `--pick <n>` | 多个可用候选时选第 n 个（1-based，Z 序从上到下） |

匹配到 0 个窗口 → 退出码 `3`，并列出当前可见窗口帮你排查。
匹配到多个可用窗口且没给 `--pick` → 退出码 `3`，打印候选表。

---

## 窗口策略

| 选项 | 说明 |
| --- | --- |
| `--focus-policy gentle`（默认） | `SetForegroundWindow` → 回读 `GetForegroundWindow` 验证 → 最多 3 次 |
| `--focus-policy none` | 目标必须**已经**是前台窗口，否则拒绝 |
| `--focus-attempts <n>` | 温和模式的尝试次数（默认 3） |
| `--allow-restore` | 允许自动还原最小化窗口（默认拒绝） |
| `-wx <cx> -wy <cy>` | 客户区坐标；必须成对出现且必须带选择器。`mouse drag` 里它是**起点**，终点用 `--wx2 <cx2> --wy2 <cy2>`（四个必须一起给，不允许与四个位置参数混用） |
| `--strict-point` | 额外要求该屏幕点下方的窗口就是目标本身（对拖拽的起止点都检查） |

---

## run（脚本）

```
KeyMouse run <文件|-> [选项]
```

一行一条命令，语法与命令行**完全一致**——没有第二套语言要学，选择器和闸门逐行生效。
用 `-` 从 stdin 读取。

```text
# 注释和空行会被跳过
sleep 400
mouse click left -wx 200 -wy 200 --process notepad
key type "hello 中文也可以" --process notepad
key press enter --process notepad
```

### 选项

| 选项 | 说明 |
| --- | --- |
| `--delay MS` | 每条命令之间等这么久（默认 0） |
| `--keep-going` | 出错也继续跑完（默认首错即停） |
| `--dry-run` | 只做解析与资格检查，**一个字节都不发**，也不抢焦点 |
| `--echo` | 连子命令自己的输出也打出来 |
| `--retry N` | 单条命令最多额外重试 N 次，**只对"确定没发出任何输入"的失败重试**（退出码 3/4/5） |
| `--retry-delay MS` | 重试前等多久（默认 300） |
| `--set name=value` | 定义变量，脚本里用 `${name}` 引用（可重复给） |
| `--report file.json` | 写一份机器可读的执行报告 |

### 一个进程，从头到尾

脚本里的**每一条命令都在启动它的那个进程内派发**，彼此之间不会重新启动 KeyMouse。
这不是实现细节，而是三条能力的来源：

- `window focus` 的目标能被后面的命令继承（状态在内存里，不需要任何磁盘上的"当前目标"文件）；
- 循环变量、`--set` 变量在展开过程中就地生效；
- 没有每次调用的进程启动开销——实测 40 行脚本全程只有 1 个 KeyMouse 进程。

回归保护：单元测试注入一个 dispatcher 并数调用次数（子进程方案下这个委托根本不会被调到），
冒烟测试在脚本运行期间采样进程数并断言恒为 1。

### 目标继承（脚本内）

脚本里**只需要写一次选择器**：

```
window focus --title "记事本"
key type "第一行"
key press enter
key type "第二行"
mouse click left -wx 100 -wy 100
```

规则：

- 任何一行只要带了**目标选择器**（`--title` / `--title-exact` / `--class` / `--process` /
  `--pid` / `--hwnd` / `--pick`），它就成为脚本的**当前目标**；最后一次写的覆盖之前的；
- 之后的 `mouse` / `key`（含 `keyboard`）行**不带选择器时自动继承**它；
- 行里自己写了选择器，就以自己的为准（并同时更新当前目标）；
- **继承不是降低验证**：继承来的目标每一行都要重新过一遍**资格闸门 + 焦点验证**，
  失败照样以退出码 `3/4/5` 停在那一行；
- `waitfor` / `waitgone` / `window list` **不继承**——等哪个窗口、列出哪些窗口，
  是值得明说的意图；
- 日志会标出继承：`key type "第二行"   ← 继承目标 --title "记事本"`，
  报告 JSON 里的 `command` 是**实际执行**的完整命令，另有 `inheritedTarget` 字段。

> 跨**进程**（`KeyMouse key type ...` 分开敲）不继承：每次调用都是独立进程，
> 没有"上一次"；而且靠隐藏状态记住目标，正是这个工具最该避免的失败模式
> （输入发到了你以为不是它的窗口）。

### 循环

```
repeat 5              # 循环变量默认叫 i，从 0 开始
  key press f24
end

repeat 3 as row       # 也可以自己命名
  key type "第 ${row} 行"
  key press enter
end
```

| 规则 | 说明 |
| --- | --- |
| 语法 | `repeat <次数> [as <名字>]` … `end`，`end` 单独一行、不带参数 |
| 循环变量 | `${i}`（或 `as` 给的名字），**从 0 开始**，每次迭代递增 |
| 嵌套 | 支持；**内层必须 `as` 取名**——两个都叫 `i` 会互相遮蔽，直接报错而不是默默按内层算 |
| `repeat 0` | 整块跳过（临时停用一段而不删除） |
| 上限 | 单层最多 1000000 次；**展开后总命令数超过 100000 条直接拒绝**，不会跑飞 |
| 结构检查 | 不配对、次数非法、`end` 带参数、变量名非法、多余的 `end`——**全部在第一条命令执行之前**报错并给行号 |
| 组合 | 循环体内每条命令照样各自过闸门与焦点验证；`--delay` / `--dry-run` / `--report` / 安全重试都照常生效 |

日志会标出当前迭代，进度分母是**展开后**的条数：

```
C:\...\demo.txt 共 10 条命令，循环展开后 16 条
[  4/16] 成功  [row=0] key type "第 0 行"   ← 继承目标 --title "记事本"
```

报告 JSON 里对应 `iterations` 字段：`"iterations": { "row": 0 }`。

### 伪命令

| 伪命令 | 说明 |
| --- | --- |
| `sleep <ms>` | 等待 |
| `waitfor <选择器> [--timeout MS] [--interval MS]` | 等到**有**一个通过闸门的窗口匹配（默认 5000ms 超时 / 200ms 间隔）；超时 → 退出码 `3` |
| `waitgone <选择器> [--timeout MS] [--interval MS]` | 等到**没有**匹配的窗口；`--timeout 0` = 立刻断言 |

`waitfor` / `waitgone` 走和其他命令**同一套**解析与闸门（`WindowResolver`）。
`--dry-run` 下它们**不会真的等**：什么都没发，UI 不可能变，所以只检查一次并报告"本来会等多久"。

### 变量

```
key type "${text}" --process ${app}
```
```powershell
KeyMouse run demo.txt --set app=notepad --set "text=你好 世界"
```

替换发生在**分词之后**，所以带空格的值仍然是**一个参数**。
未定义的变量（哪怕一次 `--set` 都没给）会**带行号报错**，不会把 `${x}` 原样漏给下游命令。

### 执行报告

```json
{
  "script": "D:\\demo.txt",
  "total": 3,
  "succeeded": 2,
  "failed": 1,
  "retriedCommands": 1,
  "injectedEvents": 14,
  "suppressedEvents": 0,
  "exitCode": 3,
  "stoppedAtLine": 3,
  "commands": [
    { "index": 1, "line": 2, "command": "key type \"hi\" --process notepad",
      "exitCode": 0, "attempts": 1, "durationMs": 412, "injectedEvents": 4, "output": "已输入 2 个字符" }
  ]
}
```

`injectedEvents` 是**真实注入到系统输入队列的事件数**，`--dry-run` 时全程为 0 ——
报告本身就是"到底有没有发东西"的证据。JSON 用 `UnsafeRelaxedJsonEscaping` 写出，中文不会变成 `\uXXXX`。

### 规则

- 行内 `"引号"` 把空格包成一个参数；`\` 可转义 `"` 和 `\`；`#` 之后是注释；`--` 显式结束选项解析。
- 脚本文件必须是 **UTF-8**：ANSI/GBK 会明确报错，不会静默变成乱码。
- 退出码 = 第一条失败命令的退出码；`--dry-run` 全通过则返回 0。
- 所有伪命令的语法错误都会**在开跑之前**报出（带行号），不会跑一半才炸。
- 脚本里不能再 `run` 另一个脚本（拒绝嵌套）。
- 样例见 [`samples/notepad-demo.txt`](../samples/notepad-demo.txt)。

---

## 例子

```powershell
# 绝对坐标
KeyMouse mouse move 100 200
KeyMouse mouse click right -x 640 -y 480
KeyMouse key combo ctrl+shift+s

# 指定窗口：先验证、再聚焦、再操作
KeyMouse key type "hello 世界" --process notepad
KeyMouse mouse click left -wx 120 -wy 340 --title "记事本"
KeyMouse mouse drag -wx 5 -wy 130 --wx2 900 -wy2 300 --process notepad
KeyMouse window list --process QQ
KeyMouse window inspect --title "记事本"
KeyMouse window focus --hwnd 0x2079C

# 多候选时显式指定（否则报错并列出候选）
KeyMouse window focus --process explorer --pick 3
```

---

## 退出码

| 码 | 含义 |
| --- | --- |
| 0 | 成功 |
| 1 | 运行时失败（如 `SendInput` 被 UIPI 拦截） |
| 2 | 参数错误 |
| 3 | 选择器无匹配 / 多候选未 `--pick` / `waitfor` 超时 / **选区被取消**（`region pick`、`probe --pick-region`） |
| 4 | 目标不可用（隐藏 / 最小化 / 被 DWM cloak / 无响应 / **被禁用**） |
| 5 | **焦点验证失败 —— 未发送任何输入** |
| 6 | **没看清**（`probe`：N 次读取不一致 / 置信度低于地板 / 没有可用引擎）—— 同样未发送任何输入 |

脚本返回**第一条失败命令**的退出码。

`3` / `4` / `5` / `6` 共享同一条承诺：**确定没有发出任何输入**，因此都可以安全重试（`--retry` 也只对它们生效）。

## probe（读，不是发）

`probe` 把窗口的一块区域读成文字。它**只报告看到什么，不做判断**——判断是调用方的事。

```
KeyMouse probe --title "记事本" --region 10,60,400,30
KeyMouse probe --title "记事本" --region 10,60,400,30 --json
```

| 选项 | 默认 | 说明 |
| --- | --- | --- |
| `--region x,y,w,h` | 整个客户区 | 区域坐标；越界一律拒绝，**不猜也不截断** |
| `--space client\|window` | `client` | `window` 用于标题栏（它在客户区之外） |
| `--reads N` | 2 | 重复读取次数，**N 次逐字一致**才算看清 |
| `--lang` | `eng+chi_sim` | 交给引擎的语言 |
| `--engine` | `tesseract` | 可执行文件；会先找常见安装位置。**目前按 Tesseract 的命令行调用**（`<引擎> <图> <输出基> -l <语言> --psm 6 [--tessdata-dir <目录>] tsv`），别家引擎要兼容这套参数才能直接用 |
| `--tessdata-dir` | `%LOCALAPPDATA%\KeyMouse\tessdata` | 模型目录，**两套模型就是两个目录** |
| `--min-conf N` | 30 | 置信度地板，只用来抓"彻底没读出来" |
| `--scale` / `--pad` | 3 / 16 | 放大与白边，实测必需（屏幕文字约 96 DPI，引擎舒适区约 300 DPI） |
| `--resample` | `nearest` | 放大时怎么取样：`nearest` 把每个源像素复制成 N×N 方块（**不发明像素**）；`bilinear` 更柔和。两者在真实语料上没量出精度差别（见下） |
| `--capture` | `screen` | `print` 用 PrintWindow（不画光标，但本机几何对不齐，见 design.md）；**`print` 与 `--space window` 不能同用**（退出码 `2`） |
| `--pick-region` | 关 | 先打开全屏选区浮层（拖动框选 / 单击选整个客户区 / `ESC` 取消），再读选中的那块；**不要再给选择器、`--region` 或 `--space`** |
| `--json` | 关 | 输出引擎身份、几何、逐词置信度 |
| `--keep-image <路径>` | 关 | **原样**保存抓到的像素：不放大、不加白边、不做对比度归一化——截图就是屏幕上那一块 |
| `--keep-prepared <路径>` | 关 | 保存送进引擎的那张图（放大 + 白边 + 归一化之后），用来解释"为什么读成这样" |

**读不到就是 `6`，读到空是成功**：区域内确实没有文字时退出码为 `0`、`lines` 为空——空是一个事实，不是失败。

**几处实测行为（照实写，省得踩）**：

- `--capture screen`（默认）抓的是**屏幕像素**：目标被别的窗口盖住时，读到的是盖住它的内容；
  `print` 走 `PrintWindow`，不画光标、也不受遮挡影响，但本机几何对不齐（会裁掉文字上半截，见 design.md）。
- **最小化窗口读不了**：它在取像之前就被闸门拒掉（退出码 `4`），换 `--capture print` 也一样
  ——要么先 `--allow-restore`，要么由调用方自己还原它。
- **壳窗口可能没有客户区**：WebView / Electron 的 `*-siw` / `*-sic` 类窗口实测 `0×0` 客户区，
  这时任何区域都判越界（退出码 `2`）。
- 量级参考：记事本一条 600×80 的区域，`--reads 2`、退出码 `0`、置信度 76.9、耗时 689 ms。
- `--engine` 目前只有 Tesseract 真正跑过；JSON 里 `kind` 对任何外部命令都写 `external`，
  引擎身份由 `command` / `version` / `models` 自证。
- **取样方式在真实语料上没有量出差别**：10 个样本 × 5 组配置（`tests/evidence/` 里有全部截图与 JSON），
  逐字正确 8~10/10——双线性 10/10、最近邻 9/10、完全不放大 9/10，平均 conf 88~94；
  差别只有 1 个样本，别据此挑配置。默认选 `nearest` 的理由是**它不发明像素**（送进引擎的图里不会有屏幕上不存在的光晕），
  不是"更准"。早先那次"最近邻明显更准"的测量把光标噪声算了进去，**已作废**。
- **读的区域必须避开文字光标**：光标会闪，两次采集因此不一致 → 退出码 `6`；更糟的是光标进画面能让整行读崩：
  `你好，世界` 读对（conf 84.7），同一块**加上光标**读成 `Re,Hh`（conf 49.8）——两帧都留档在 `tests/evidence/`。
- **区域高度要盖住整行**：`0,0,400,20` 把字切掉，同一行读成 `4eim+s`；`0,0,400,32` 就正常。
- `--pick-region` 选到标题栏（窗口坐标）时不能配 `--capture print`（退出码 `2`）；取消选区（`ESC` / 右键）
  是退出码 `3`，一个字也没读。

## region（选区：给人挑坐标，不读文字）

坐标写不准是个真问题：实测里三次凭记忆挑区域，三次都落在壁纸或空白上。`region pick` 把这件事交给鼠标——
**拖动框选**，或**单击选中某个窗口的整个客户区**，`ESC` / 右键取消。它**只报坐标**，
并给出一条可以直接粘贴的 `probe` 命令。

```
KeyMouse region pick                          # 框一块区域
KeyMouse region pick --json                   # 机器可读：screen / window / space / region / probe
KeyMouse region pick --rect 100,200,300,40    # 跳过浮层，屏幕坐标（脚本与测试用）
```

| 选项 | 默认 | 说明 |
| --- | --- | --- |
| `--rect x,y,w,h` | 打开浮层 | **屏幕坐标**的非交互入口；宽高必须为正 |
| `--json` | 关 | 输出 `screen` / `window` / `space` / `region` / `probe` |

坐标系只有一条判定规则：**整个选区落在客户区里 → `client`；落在窗口矩形里（标题栏）→ `window`；
两者都不是 → 只报屏幕坐标并说明原因**（`probe` 只接受前两种，所以这种情况不会给出可粘贴的命令）。

```
选区  屏幕 700,470 260x40
目标  0x1D097E  Notepad(31612)  "新建 文本文档.txt - Notepad"  类名=Notepad  [可见，响应=0ms]
空间  客户区（client）  region 60,200,260,40
命令  KeyMouse probe --hwnd 0x1D097E --region 60,200,260,40
```

浮层里几条**被实测逼出来**的细节（改它之前先看 design.md 的实测记录）：

- 打开前先抓一张**冻结的屏幕快照**当背景：瞄准的东西不会动，浮层自己也不会被抓进图里。
- 浮层必须真的在最顶层——只设 `TopMost` 不够，实测它仍在目标窗口**下面**，拖动落到了目标窗口上。
- 取消键是**全局热键**：后台进程抢不到前台，只在有焦点时才灵的取消键不算取消键。
- 线程必须是 Per-Monitor V2 并且**开浮层前验证**：WinForms 会把 UI 线程降成 SystemAware，
  那样量到的桌面是逻辑尺寸（实测 2560×1440 vs 真实 5120×1532），交出来的坐标整体被缩放；
  验证不过就退出码 `4`，宁可不给坐标。

**它不做的事**：不判断"这算不算匹配"、不做容错匹配、不看颜色、不等状态变化。置信度只回答"到底读出来了没有"：
实测读对时 55~96、读错时 14~52，两者**重叠**——所以精确与否必须由调用方用容错匹配声明，而不是靠提高阈值。

