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
| 7 | **被客户端取消**（`runner cancel` / 编辑器"停止"）——取消点之后未发送任何输入 |
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
| `--scale` / `--pad` | 1 / 0 | **默认把抓到的原始像素直接交给引擎**：不放大、不加白边。要放大时 `--scale 3 --pad 16` 是实测过的组合 |
| `--normalize` | 关 | 先把对比度拉到黑白（含暗底反相）。暗底浅字的窗口读不出来时用；实测深底白字**不归一化也能读**（见下） |
| `--resample` | `bilinear` | 放大时的取样方式：`bilinear` 插值；`nearest` 把每个源像素复制成 N×N 方块（不发明像素）。两者在真实语料上没量出精度差别（见下） |
| `--capture` | `screen` | `print` 用 PrintWindow（不画光标，但本机几何对不齐，见 design.md）；**`print` 与 `--space window` 不能同用**（退出码 `2`） |
| `--pick-region` | 关 | 先打开全屏选区浮层（拖动框选 / 单击选整个客户区 / `ESC` 取消），再读选中的那块；**不要再给选择器、`--region` 或 `--space`** |
| `--json` | 关 | 输出引擎身份、几何、`prepared`（引擎拿到的是原图还是变换过的图）与逐词置信度 |
| `--keep-image <路径>` | 关 | **原样**保存抓到的像素：不放大、不加白边、不做对比度归一化——截图就是屏幕上那一块 |
| `--keep-prepared <路径>` | 关 | 保存送进引擎的那张图（放大 + 白边 + 归一化之后），用来解释"为什么读成这样" |

**读不到就是 `6`，读到空是成功**：区域内确实没有文字时退出码为 `0`、`lines` 为空——空是一个事实，不是失败。

**几处实测行为（照实写，省得踩）**：

- **读之前别留选区**：选中反色时同一行的置信度从 **92.0 掉到 65.0**，还会多读字符（`SelectionTest` →
  `ISelectionTest`）甚至读空；要读就先点一下区域外：既取消选区，也把光标挪出区域。

- `--capture screen`（默认）抓的是**屏幕像素**：目标被别的窗口盖住时，读到的是盖住它的内容；
  `print` 走 `PrintWindow`，不画光标、也不受遮挡影响，但本机几何对不齐（会裁掉文字上半截，见 design.md）。
- **最小化窗口读不了**：它在取像之前就被闸门拒掉（退出码 `4`），换 `--capture print` 也一样
  ——要么先 `--allow-restore`，要么由调用方自己还原它。
- **壳窗口可能没有客户区**：WebView / Electron 的 `*-siw` / `*-sic` 类窗口实测 `0×0` 客户区，
  这时任何区域都判越界（退出码 `2`）。
- 量级参考：记事本一条 600×80 的区域，`--reads 2`、退出码 `0`、置信度 76.9、耗时 689 ms。
- `--engine` 目前只有 Tesseract 真正跑过；JSON 里 `kind` 对任何外部命令都写 `external`，
  引擎身份由 `command` / `version` / `models` 自证。
- **默认把原始像素交给引擎**（`--scale 1 --pad 0`，不归一化）：屏幕上是什么，引擎就读什么。
  20 个真实样本 × 5 组配置（本地留档 `tests/evidence/`，不入库）：**原始像素 12/20、原始+归一化 12/20、
  3× 最近邻 11/20、3× 双线性 9/20、3× 双线性+归一化 15/20**（平均 conf 89~92）。
  也就是说**放大+归一化确实更准**，但它不是默认——想换就 `--scale 3 --pad 16 --normalize`。
- **取样方式（放大时）没量出稳定差别**：`nearest` 与 `bilinear` 在 10 个样本那批差 1 个样本，
  在 20 个样本那批差 2 个；早先那次"最近邻明显更准"的测量把光标噪声算了进去，**已作废**。
- **深底浅字不归一化也能读**：拿我们自己的选区浮层当样本（白字深底的提示条），
  原始像素 conf 72~74、加 `--normalize` 75~79——`--normalize` 是加分项，不是必需品。
- **读的区域必须避开文字光标**：光标会闪，两次采集因此不一致 → 退出码 `6`（`probe` 不做任何文字归一化，
  两次读取必须逐字节相同）；更糟的是光标进画面能让整行读崩：`你好，世界` 读对（conf 84.7），
  同一块**加上光标**读成 `Re,Hh`（conf 49.8）——两帧都留在本地留档 `tests/evidence/` 里。
- **区域高度要盖住整行**：`0,0,400,20` 把字切掉，同一行读成 `4eim+s`；`0,0,400,32` 就正常。
- `--pick-region` 选到标题栏（窗口坐标）时不能配 `--capture print`（退出码 `2`）；取消选区（`ESC` / 右键）
  是退出码 `3`，一个字也没读。

| `--find <文字>` | 关 | **找文字并给出点击点**：在读到的东西里定位这段文字，报告它的框与中心点（客户区坐标）；没找到 → 退出码 `3`（没发任何输入，可重试） |
| `--match <方式>` | `contains` | `--find` 的比较方式：`contains` / `exact` / `fuzzy`（编辑距离 ≤ `--max-errors`） |
| `--max-errors N` | 1 | `fuzzy` 的容错预算（按字符计） |

`--find` 的存在是因为"读到"和"点到"之间差一个坐标：引擎给的是**每个词的框**（`lines[].words[].rect`，
相对区域），`--find` 把它变成可点的中心点。多词匹配（`取消确定` 这种跨词的串）取这些框的并集，
所以 `lines[].rect` 也是并集而不是第一个词的框。JSON 里多一个 `find` 对象：

```json
"find": { "text": "取消", "found": true, "match": "contains", "rect": [79,7,51,22],
          "at": { "space": "client", "x": 145, "y": 18 }, "line": "保存 取消 确定",
          "distance": 0, "confidence": 91.0 }
```
## record（录制：把一次操作变成可回放的 JSON）

```
KeyMouse record --out flow.json                 # 开始录，Ctrl+Alt+Q 停止（连按两次 ESC 也停）
KeyMouse record --out flow.json --duration 10000
KeyMouse run flow.json                          # 回放
```

全局监听键盘与鼠标（`WH_KEYBOARD_LL` / `WH_MOUSE_LL`），**只观察、不拦截**：你照常操作，
记录下来的每个动作都带三样东西——**它发生在哪个窗口**（进程 + 类名，标题另存供人看）、
**客户区相对坐标**（客户区之外的点击记为屏幕坐标并写明原因）、以及**与上一步的间隔**
（超过 `--min-gap` 就落成一个 `sleep` 步骤）。

| 选项 | 默认 | 说明 |
| --- | --- | --- |
| `--out <路径>` | `flow.json` | 流程文件；截图放在同目录的 `<名字>.shots/` |
| `--duration <毫秒>` | 关 | 到点自动停（无人值守/自动化用；也可以按停止键） |
| `--no-shots` | 开 | 不存关键步骤的截图 |
| `--min-gap <毫秒>` | 300 | 多长的间隔值得记成 `sleep` |
| `--move-threshold <像素>` | 8 | 鼠标轨迹按这个距离抽稀（越短越忠实、文件越大） |

**停止键**：`Ctrl+Alt+Q`，或连按两次 `ESC`。停止键本身不会被录进流程。

**录进去的步骤**：`focus`（切换窗口）、`click`、`drag`（按住移动超过阈值就是一个 drag，
带起点/终点/时长）、`move`（抽稀后的轨迹）、`wheel`、`type`（连续输入的字符合成一步，
并记下你实际的输入速度）、`key`（特殊键与组合键）、`sleep`。

**两条要记住的事**：

- 录制期间**你敲的每个键都会被记录**（包括密码这种敏感内容），流程文件是明文，
  关键步骤的截图也可能含敏感画面——录完自己看一眼再分享。
- 回放走的还是同一条路：流程被编译成一条条命令，**选择器、焦点闸门、退出码语义与手打命令完全一致**，
  `--dry-run` / `--report` / `--retry` 也照常工作；流程里可以加条件步骤（`sleep` / `wait-window` /
  `wait-text`，见下一节），它们是"等到什么"而不是"再敲一下"。

## 流程 JSON（`keymouse-flow`）

```json
{
  "format": "keymouse-flow",
  "version": 1,
  "recordedAt": "2026-10-03T18:10:23+08:00",
  "screen": { "width": 2560, "height": 1440 },
  "options": { "minGapMs": 300, "moveThresholdPx": 8, "shots": true },
  "steps": [
    { "type": "click", "button": "left", "at": { "space": "client", "x": 120, "y": 159 },
      "target": { "process": "notepad", "class": "Notepad", "title": "新建 文本文档.txt - Notepad" },
      "shot": "flow.shots/0001-click.png" }
  ]
}
```

| `type` | 字段 | 回放成 |
| --- | --- | --- |
| `focus` | `target` | `window focus` |
| `click` | `button`、`at` | `mouse click` |
| `move` | `at` | `mouse move`（屏幕坐标时是位置参数写法） |
| `wheel` | `delta`、`at` | `mouse wheel`（连续滚动会合并成一个 delta） |
| `drag` | `from`、`to`、`durationMs` | `mouse drag --duration` |
| `type` | `text`、`intervalMs` | `key type --interval` |
| `key` | `combo`（组合）或 `text`（单个键） | `key combo` / `key press` |
| `sleep` | `ms` | 执行器直接等（`--dry-run` 时不等） |
| `wait-window` | `target`、`timeoutMs`、`intervalMs` | 执行器轮询到窗口可用；超时退出码 `3` |
| `click-text` | `target`、`region`、`text`、`match`、`maxErrors`、`button`、`timeoutMs` | **看到就点它**：等文字出现（同 `wait-text` 的确认规则），然后点**匹配框的中心**；没等到 → 退出码 `3` || `wait-text` | `target`、`region`、`text`、`match`、`maxErrors`、`timeoutMs`、`intervalMs`、`confirm` | 执行器**读区域等文字**：每轮一次 OCR，连续 `confirm` 次读到**同一段**满足条件的文字才算等到；超时退出码 `3` |
| `read-text` | `target`、`region`、`into`、`text`（可选） | 读一次区域，把内容存进变量（给了 `text` 就存匹配到的那段）；没匹配到 → 退出码 `3`，变量不动 |
| `call` | `flow`、`vars`、`export` | 运行另一个流程文件：**内联**执行，子流程有自己的变量作用域，`export` 里的名字交回调用者 |

### wait-text：等一块区域上出现某段文字

```json
{ "type": "wait-text",
  "target": { "process": "notepad" },
  "region": { "space": "client", "x": 0, "y": 0, "width": 600, "height": 32 },
  "text": "保存成功", "match": "fuzzy", "maxErrors": 1,
  "timeoutMs": 8000, "intervalMs": 250, "confirm": 2 }
```

| 字段 | 默认 | 说明 |
| --- | --- | --- |
| `region` | 必填 | 读哪一块（`space` 只支持 `client`：屏幕坐标会随窗口移动失效）。越界在**加载时**就报错（退出码 2） |
| `text` | 必填 | 等什么字 |
| `match` | `contains` | `contains`（读到里含期望）/ `exact`（完全一致）/ `fuzzy`（编辑距离 ≤ `maxErrors`） |
| `maxErrors` | 1 | 只对 `fuzzy` 有意义。**这是你声明的容错预算，不是工具偷偷放宽的阈值** |
| `timeoutMs` | 5000 | 等多久；超时退出码 `3`（= 没做任何操作，可安全重试） |
| `intervalMs` | 250 | 两轮之间等多久（每轮都是一次真实的引擎调用，约 0.2~0.6 秒） |
| `confirm` | 2 | 需要连续几轮**读到同一段**满足条件的文字才算数；设 1 就是"一眼看到就算" |

三条语义值得记住：

- **比较时忽略空白**，两侧都是。引擎把中文按字切词并加空格（`你 好 ， 世 界`），
  若按字面比较，你写屏幕上的原文 `你好，世界` 反而永远不匹配。**读本身没有被加工**：
  `probe` 打印的仍是引擎原样输出。
- **`confirm` 是可信度的来源**：单次读取可能正好撞上重绘/光标（实测同一个区域两次读取会不一致），
  连续两次读到同一段才算"等到了"。
- `--dry-run` 下不读：只解析窗口并校验区域是否越界，然后报告"会等什么、等多久"。

超过 `confirm` 次仍未等到就是退出码 `3`，并且失败信息会告诉你**最后读到的是什么**、
离期望差几个字符——这才是这个条件能被调试的原因。

`at` / `from` / `to` 的 `space` 只认 `client`（客户区相对，窗口一动也不失效）与 `screen`
（绝对屏幕坐标，窗口移动后就失效——录制器只在客户区之外才用它，并会写进 `note`）。
`target` 里的 `process` + `class` 是回放真正用的选择器；`title` 只给人看。

---

## serve（常驻 Runner）与 runner（让 CLI 去驱动它）

```
KeyMouse serve                      # 前台启动常驻 Runner（编辑器连的就是它）
KeyMouse runner status              # 问它现在什么状态、有哪些作业
KeyMouse runner run flow.json       # 让常驻 Runner 执行一个流程（CLI 只是客户端）
KeyMouse runner cancel 3            # 暂停 / 继续 / 取消某个作业
KeyMouse runner pause 3
KeyMouse runner resume 3
KeyMouse runner stop                # 请它退出
```

**一个产品、两个入口、三层**：编辑器（WinUI）与命令行都连同一个常驻 Runner，Runner 驱动的是与
一次性命令**完全相同**的那份派发——所以退出码、闸门、焦点验证不会因为入口不同而变。

- 管道：`\\.\pipe\keymouse-runner-<用户名>`（按用户隔离，没有端口）。
- 协议：一行一个 JSON 对象，双向。请求 `{"id":1,"method":"run","params":{"flow":"...","dryRun":false}}`；
  事件 `{"kind":"..."}`，`kind` ∈ `hello | log | step | finished | result | error`。
  `step` 带 `index/total/state(started|finished)/type/exitCode/durationMs`——编辑器就是靠它在列表里
  实时高亮正在执行的那一步。
- 方法：`hello | status | list | run | record | validate | pick-region | ocr | cancel | pause | resume | shutdown`。
- **一次只跑一个作业**：输入是全局资源，作业排队而不是并行（理由见 design.md）。

退出码 `7 = 被客户端取消`：取消点之后没有再发出任何输入（和 3/4/5/6 一样，可以放心重跑）。

## flow（图形编辑器）

```
KeyMouse flow edit flow.json                    # 用 WinUI 编辑器打开一个流程
```

编辑器在 `editor\KeyMouse.FlowEditor`（独立项目，**要单独构建**：
`dotnet build editor\KeyMouse.FlowEditor -c Release`）。它做四件事：

- 左边是**步骤列表**：拖拽排序、上移/下移/复制/删除，每条显示类型、一行摘要和录制时的缩略图
  （缩略图是**整个客户区**，动作点用红十字标出来——320×200 的原地裁剪实测在空白窗体上就是一块白）。
- 右边是**参数检查器**：按步骤类型只显示该有的字段，另有"这一步的 JSON"随时可看。
- **从屏幕取区域**：拉的是 `region pick` 那个浮层，选完把矩形与窗口直接填进当前步骤。
- **试运行/播放**：存一份给 `run`（未保存的改动写到临时文件，不覆盖你正在编辑的文件），
  输出实时显示在「运行输出」里，退出码按 CLI 的同一套含义解释。

编辑器**不重新实现**录制、取区域、读取与回放——那些行为（连同它们的实测结论）都只在命令行这边有一份。


### `when`：任何一步都可以带前提

```json
{ "type": "click", "at": { "space": "client", "x": 200, "y": 300 },
  "target": { "process": "notepad" },
  "when": { "target": { "process": "notepad" },
            "region": { "space": "client", "x": 0, "y": 0, "width": 600, "height": 32 },
            "text": "保存成功", "match": "contains", "timeoutMs": 2000, "else": "skip" } }
```

`when` 是这套格式里唯一的条件：前提成立才执行这一步。

- `target` 可以自己给（**可以盯另一个窗口**）；不给就用步骤自己的 target。
- `else`：`skip`（默认，跳过这一步、退出码仍是 0）或 `fail`（退出码 `3`，同样保证"什么都没发"）。
- 前提不成立时的跳过会在报告里标成 `skipped`，控制台那一行显示 `skip`。
- 一串步骤各自带 `when`，就是最常用的分支写法；真正的 if/else 块与循环留给后续版本。



### 子流程（call）

```json
{ "type": "call", "flow": "sub/login.json",
  "vars": { "user": "{{item}}" },      // 传给子流程（可引用调用者的变量）
  "export": ["seen"] }                 // 子流程跑完后，把 seen 交回调用者
```

- **`flow`** 相对于**调用它的那个文件**（嵌套调用同理），所以整个目录可以整体搬走。
- **作用域是链式的**：子流程看得到调用者的变量，也能用自己的 `variables` 覆盖；`vars` 优先级最高。
- **子流程里的 `read-text` 捕获留在子流程内**，不会悄悄改掉调用者的同名变量——要交回来就写 `export`，而且只有 `export` 列出的名字会回来。
- **加载时就查**：文件不存在、`flow` 缺失、调用成环（a → b → a）、嵌套超过 8 层，都是退出码 `2` 并指出是第几步。
- 一次 `call` 的步骤会**内联**进这次运行：日志与事件里的编号是"第几步"（按实际要跑的步数），不是文件里的行号。

### 变量与循环

```json
{ "format": "keymouse-flow", "version": 1,
  "variables": { "app": "notepad", "rows": ["第一行", "第二行", "第三行"] },
  "steps": [
    { "type": "focus", "target": { "process": "{{app}}" } },
    { "type": "foreach", "in": "rows", "steps": [
        { "type": "type", "text": "{{item}}", "target": { "process": "{{app}}" } },
        { "type": "key", "text": "enter", "target": { "process": "{{app}}" } } ] },
    { "type": "repeat", "times": 3, "steps": [ { "type": "sleep", "ms": 200 } ] } ] }
```

- **`variables`**：值是字符串（`{{名字}}` 用）或字符串数组（`foreach` 遍历）。
  `KeyMouse run flow.json --set app=mspaint` **覆盖**文档里的同名值（数组会被压成单元素，这是明说的取舍）。
- **`{{名字}}`** 只能出现在**字符串**字段（文字、组合键、进程名/类名/标题、备注）——JSON 的数字放不下占位符。
  替换发生在**派发前一刻**，所以 `read-text` 刚捕获的值，后面的步骤立刻就能用。
- **循环变量**：`repeat`/`foreach` 里可用 `{{index}}`（0 起），`foreach` 里还有 `{{item}}`。
- **`times` 上限 10000**：循环不能无限跑，这条上限写在文档里，而不是跑到凌晨三点才发现。
- **分组自己不能带 `when`**（展平后它没有落脚点）：把前提写到里面的步骤上；循环体里再嵌套循环按次数相乘。
- 变量名写错、`foreach` 遍历了不存在的列表，都在**加载时**以退出码 `2` 报出来，并指出是第几步。


### 还原最小化窗口（同意语义）

目标窗口最小化时，KeyMouse 不会擅自把它还原——**还原是在改你的桌面**，所以必须明说：

- 命令行：`KeyMouse window focus --process 某进程 --allow-restore`，或 `KeyMouse run flow.json --allow-restore`
- 流程文件：`{ "format": "keymouse-flow", "version": 1, "allowRestore": true, ... }`

两者**任一**即可；都不说时 `focus` 以退出码 `4` 拒绝，并在信息里直接告诉你怎么允许。

### 聚焦是"验证过的"，不是"喊过了"

`focus` 不会假定调用成功：它会**回读真实的前台窗口**，只有前台真的变成了目标才返回 0。如果窗口拒绝到前台，
退出码 `5`，信息里会点名**当前**占着前台的是谁。

- 机制：先 `AttachThreadInput` 把当前前台线程的输入队列接到自己身上，再 `SetForegroundWindow`。
  这是 Windows 前台锁的官方绕法，而且**不注入任何按键**——不会往别的窗口打字。
- 若仍然失败：目标以管理员身份运行时，UIPI 会挡住前台切换与输入注入，KeyMouse 也需要以管理员身份运行。

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

