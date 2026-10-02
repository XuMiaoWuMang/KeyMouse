# KeyMouse 命令参考

完整的命令、选项与退出码。概览见 [README](../README.md)，设计取舍见 [design.md](design.md)。

## 调用形式

```
KeyMouse <group> <command> [参数] [选项]
```

四组命令：`mouse`、`key`（别名 `keyboard`）、`window`、`run`。无参数运行会打印内置帮助。

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
| 3 | 选择器无匹配 / 多候选未 `--pick` / `waitfor` 超时 |
| 4 | 目标不可用（隐藏 / 最小化 / 被 DWM cloak / 无响应 / **被禁用**） |
| 5 | **焦点验证失败 —— 未发送任何输入** |

脚本返回**第一条失败命令**的退出码。
