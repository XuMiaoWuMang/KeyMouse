# 命令行参考

运行 `KeyMouse help` 查看入口，`KeyMouse help probe` 查看一组参数。下文用 `KeyMouse` 代表实际可执行文件；未放入 PATH 时用完整路径或 PowerShell 的 `& $km`。命令分为读取状态、执行输入和运行流程三类。

## 输出编码

交互控制台默认使用当前控制台代码页。把输出交给编辑器或其他程序解析时，设置 `KEYMOUSE_OUTPUT_ENCODING=utf-8` 可强制使用无 BOM 的 UTF-8；没有控制台时默认也是 UTF-8。

| 任务 | 命令 | 先确认什么 |
| --- | --- | --- |
| 查看可见窗口 | `KeyMouse window list --json` | 输出含窗口标题，仅供本地选择 |
| 检查流程 | `KeyMouse run samples/demo-flow.json --dry-run` | 不发送输入，不等于业务动作已成功 |
| 录制、编辑、回放 | `record`、`flow edit`、`run` | 使用空白靶窗；录制内容是明文 |
| 截取区域读字 | `region pick`、`probe` | 确定坐标空间与读图方式 |
| 指定后台控件 | `background controls` 等 | 需要受支持的控件及唯一目标，不回退到前台 |

## 录制与 JSON 回放

```powershell
# 在本地忽略目录保存录制；不要在此期间输入凭据
New-Item -ItemType Directory -Force local-recordings | Out-Null
KeyMouse record --mode key --client-points --no-shots --out local-recordings/task.json
KeyMouse flow edit local-recordings/task.json
KeyMouse run local-recordings/task.json --dry-run
```

CLI 默认 `--mode full`，编辑器默认关键录制。`--duration` 设置自动停止时间；手动可用 `Ctrl+Alt+Q` 或短时间连按两次 Esc。`--move-threshold` 控制移动轨迹抽稀。`--no-shots` 只关闭截图，不隐藏已输入的文字。没有 `--client-points` 的 CLI 录制默认使用屏幕坐标。

新录制每步写 `delayMs`，不插入 `sleep`：首步从开始等待，其余从前一步结束起算。关键录制写 1000 ms，完全录制写实际间隔。手写步骤不填默认为 1000 ms；显式 `0` 表示立即执行。旧 `atMs` 且未填 `delayMs` 的动作沿用原绝对时间戳。手动 `sleep.ms` 是该操作自身的时长；`--min-gap` 为兼容保留，不生成等待步骤。

`run --report <路径>` 生成报告。新流程的控制语句、变量、子流程、区域和步骤参数查[文件格式](interface.md)。编辑器能修改等待值，CLI 的 `--delay` 是另外的运行选项。

## mcp：标准输入输出服务

启动 `KeyMouse mcp`，客户端的程序设为实际 CLI，参数为 `["mcp"]`。默认提供 `list_windows` 和 `read_text` 只读工具；只有显式添加 `--allow-input` 才注册 `click`、`press_key` 和 `type_text`。目标 HWND 必须来自本次窗口枚举。诊断请区分协议错误和 `structuredContent.exitCode`；不要把完整窗口清单或 OCR 结果上传。

## background（后台控件，独立 CLI）

想在继续使用前台程序时操作另一个窗口，用 `background`，而不是 `key` / `mouse --focus-policy never`。目前支持标准 Windows/WinForms 编辑框和按钮（必须有独立子窗口句柄），同一交互会话、用户和权限级别。可以操作最小化目标，不需要还原。应用自身的事件处理程序仍可能弹窗或切换焦点。

**先定位，再执行**。在 PowerShell 7 中，从 `window list` 取得目标的真实 PID 和 HWND，替换下列示例值：

```powershell
KeyMouse window list --json
$targetArgs = '--pid', '4321', '--hwnd', '0x1234'
KeyMouse background controls @targetArgs
# 从 controls 返回的 JSON 取 id 或 control；以下 Notes、ApplyButton 都是示例 ID
KeyMouse background insert-text @targetArgs --id Notes --text '在当前插入点写入'
KeyMouse background set-value   @targetArgs --id Notes --text '替换全部内容'
KeyMouse background invoke      @targetArgs --id ApplyButton
```

| 命令 | 语义 |
| --- | --- |
| `controls --pid N --hwnd H` | 输出 `{pid, hwnd, controls:[{control, kind, id}]}`；只列出范围内识别出的普通编辑框、按钮，不输出编辑内容或密码控件。列出不代表当前可写/可触发。 |
| `insert-text … --text TEXT` | 插入到编辑框的当前插入点，若有选区则仅替换选区；不是逐键输入，不会触发每个字符的键盘事件。 |
| `set-value … --text TEXT` | 替换整个编辑框内容，可用空字符串清空。两个文本命令都回读核对，不回显输入文本。 |
| `invoke …` | 一次性触发按钮的 UI Automation Invoke 操作，不移动光标，不等价于坐标点击。成功只表示已提交，不证明按钮的业务任务已经完成。 |

- 每个动作必须同时给 `--pid N --hwnd H`，再选 `--id AutomationId` 或 `--control 0x子句柄` **其中一个**。ID 在该窗口内受支持控件中区分大小写精确匹配。为空或重名时，重新 `controls` 并用当次列出的句柄；不要跨窗口关闭/重建保存旧句柄。只接受分开的选项和值。
- `--text` 最多 4096 个 UTF-16 代码单元，不接受 NUL；`insert-text` 不接受空文本，也不处理已有内容达到 65535 个代码单元的编辑框。单行框拒绝换行；目标若截断、格式化或同时被其他程序修改，回读不一致会明确报错，不会再写一次。
- 退出码 `2`：参数错误；`3`：ID 无匹配或不唯一；`4`：窗口/控件不可用或权限、能力不符，未投递；`1`：动作可能已经执行，请检查目标状态，**不要自动重试**。不回退到系统键鼠输入、不隐式提权、不在失败后切回原窗口。
- 此组命令只走独立 CLI，不是流程步骤，也不开放给 Runner、编辑器、UIAccess helper 或 MCP。自绘/浏览器页面、任意坐标点击、拖拽、全局快捷键和锁屏操作不在支持范围。完整验证见[可行性报告](../dev/background-input-feasibility.md)。

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

带选择器时默认只校验窗口资格、换算坐标，不抢焦点；要先聚焦目标，显式指定 `--focus-policy gentle`（见下面的窗口策略）；`mouse pos` 不接受选择器。

---

## key

| 命令 | 说明 |
| --- | --- |
| `key press <key> [-n COUNT] [-i MS]` | 敲击，可连按（默认间隔 60ms） |
| `key down <key>` / `key up <key>` | 按下 / 抬起 |
| `key combo <k1+k2+...> [--hold MS]` | 组合键，如 `ctrl+shift+s`、`win+r`、`alt+f4`；修饰键先按、反序释放 |
| `key type <text> [--interval MS]` | 输入文本；Unicode 注入，**不依赖输入法/键盘布局**，中文直接打；默认 15ms/字，`--interval 0` 最快但可能丢字。文本为空会被拒（退出码 2）——那通常说明流程里漏了 `text` |

独立 CLI 的 `key type --stdin` 从重定向的标准输入读取完整的 UTF-8 文本，保留空格、换行和前导连字符。MCP 的 `type_text` 使用这个入口传递原文；嵌套流程和 Runner 不会读取宿主的输入流。

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
| `window list [--filter <串>] [--process <名>] [--all] [--json]` | 列出顶层窗口及其状态；默认只列**可见且有标题**的，`--all` 连隐藏/无标题的一起列。`--json` 输出 `windows` 数组 |
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

## 输入目标与权限

屏幕输入作用在交互桌面。选择器指定目标后才有目标资格与焦点检查；`--focus-policy never` 不会将普通键盘命令转换成后台控件消息。可后台回放的是有有效目标的客户区鼠标步骤；跨用户、会话或权限边界会被拒绝。最小化目标不能一概而论，见[后台验证边界](../dev/background-input-feasibility.md)。`--allow-restore` 是同意还原，不是跳过检查。

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
| `--max-steps N` | 运行的全局步数上限（默认 2000000），超限退出码 `3`；流程的条件循环与脚本的重复展开都靠它兜底 |

报告先写 `--report` 指定的路径；路径被占用、权限不够或指向一个目录时，会自动改写到
`%TEMP%\KeyMouse\editor-run-report.json`，stderr 说明原因，**退出码不变**。图形编辑器读报告时
按修改时间取主路径与备用路径里较新的一份，并在每次运行前清掉两处旧报告，避免拿到上一轮结果。

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
demo.txt 共 10 条命令，循环展开后 16 条
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
  "script": "demo.txt",
  "total": 3,
  "succeeded": 2,
  "failed": 1,
  "retriedCommands": 1,
  "injectedEvents": 14,
  "suppressedEvents": 0,
  "exitCode": 3,
  "stoppedAtLine": 3,
  "commands": [
    { "index": 1, "origin": "1", "line": 2, "command": "key type \"hi\" --process notepad",
      "exitCode": 0, "attempts": 1, "durationMs": 412, "injectedEvents": 4, "output": "已输入 2 个字符" }
  ]
}
```

`origin` 是这一步在**文档里的位置**：`"3"` 是第三个顶层步，`"6.1"` 是第六步的第一个子步，
`"6.e1"` 是第六步的「条件不符」（else）分支里的第一个子步（`.e` 标记 else，避免和条件成立分支的 `6.1` 撞号）。
循环展开成多项时同一步的 `origin` 相同（它描述"哪一行"，不是"第几次执行"），子流程内联时
沿用调用它的那一行的 `origin`——编辑器靠它把结果写回正确的行。脚本（`.txt`）没有这一步，
所以 `origin` 缺省。

流程里一旦有条件分支或条件循环，`total` 是**静态规划数**（`if` 两条分支都算、循环体只算一遍），
不保证等于实际执行步数；实际执行了多少看 `commands` 的条数 / `succeeded + failed`。没有控制流的旧流程
两个数一致。

`injectedEvents` 是**真实注入到系统输入队列的事件数**，`--dry-run` 时全程为 0 ——
报告本身就是"到底有没有发东西"的证据。JSON 用 `UnsafeRelaxedJsonEscaping` 写出，中文不会变成 `\uXXXX`。

### 规则

- 行内 `"引号"` 把空格包成一个参数；`\` 可转义 `"` 和 `\`；`#` 之后是注释；`--` 显式结束选项解析。
- 脚本文件必须是 **UTF-8**：ANSI/GBK 会明确报错，不会静默变成乱码。
- 退出码 = 第一条失败命令的退出码；`--dry-run` 全通过则返回 0。
- 所有伪命令的语法错误都会**在开跑之前**报出（带行号），不会跑一半才炸。
- 脚本里不能再 `run` 另一个脚本（拒绝嵌套）。
- 样例见 [`samples/notepad-demo.txt`](../../samples/notepad-demo.txt)。

---

## 退出码

| 码 | 含义 |
| --- | --- |
| 0 | 成功 |
| 1 | 运行时失败（如 `SendInput` 被 UIPI 拦截） |
| 2 | 参数错误 |
| 3 | 选择器无匹配 / 多候选未 `--pick` / `waitfor` 超时 / **选区被取消**（`region pick`、`probe --pick-region`）/ **循环超过 `maxRounds` 或 `--max-steps`** / **`assert` 不成立** |
| 4 | 目标不可用（隐藏 / 最小化 / 被 DWM cloak / 无响应 / **被禁用**） |
| 5 | **焦点验证失败 —— 未发送任何输入** |
| 6 | **没看清**（`probe`：N 次读取不一致 / 置信度低于地板 / 没有可用引擎）—— 同样未发送任何输入 |
| 7 | **被客户端取消**（`runner cancel` / 编辑器"停止"）——取消点之后未发送任何输入 |

脚本返回**第一条失败命令**的退出码；流程里的 `assert` / 循环护栏也是 `3`，语义同样是"确定没发出输入"。

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
| `--lang` | `eng+chi_sim` | 交给**外部**引擎的语言；内置引擎固定中文+英文，这一项对它无意义 |
| `--engine` | `auto` | `auto`＝有内置模型就用内置、没有回落到 Tesseract；`builtin`＝只用内置，没模型直接报错；`tesseract` 或任意路径＝走旧的 Tesseract 命令行（`<引擎> <图> <输出基> -l <语言> --psm 6 [--tessdata-dir <目录>] tsv`）。内置引擎的模型在 `<exe>\models\ocr\`，取法见 [fetch-ocr-models.ps1](../tools/fetch-ocr-models.md) |
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
- **引擎有两套**：内置（PaddleOCR PP-OCRv4 的 det/rec，ONNX Runtime，模型随程序走）与外部（Tesseract 命令行）。
  报告里 `kind` 写 `builtin` 或 `external`；内置那套模型缺失时 `auto` 会回落并把原因写进 `command` 字段，
  设计、参数与已知边界见 [OCR](../dev/ocr.md)。
  引擎身份由 `command` / `version` / `models` 自证。
- **默认把原始像素交给引擎**（`--scale 1 --pad 0`，不归一化）：屏幕上是什么，引擎就读什么。
  20 个真实样本 × 5 组配置（本地留档 `artifacts\tests\evidence-<时间戳>\`，不入库）：**原始像素 12/20、原始+归一化 12/20、
  3× 最近邻 11/20、3× 双线性 9/20、3× 双线性+归一化 15/20**（平均 conf 89~92）。
  也就是说**放大+归一化确实更准**，但它不是默认——想换就 `--scale 3 --pad 16 --normalize`。
- **取样方式（放大时）没量出稳定差别**：`nearest` 与 `bilinear` 在 10 个样本那批差 1 个样本，
  在 20 个样本那批差 2 个；早先那次"最近邻明显更准"的测量把光标噪声算了进去，**已作废**。
- **深底浅字不归一化也能读**：拿我们自己的选区浮层当样本（白字深底的提示条），
  原始像素 conf 72~74、加 `--normalize` 75~79——`--normalize` 是加分项，不是必需品。
- **读的区域必须避开文字光标**：光标会闪，两次采集因此不一致 → 退出码 `6`（`probe` 不做任何文字归一化，
  两次读取必须逐字节相同）；更糟的是光标进画面能让整行读崩：`你好，世界` 读对（conf 84.7），
  同一块**加上光标**读成 `Re,Hh`（conf 49.8）——两帧都留在本地留档 `artifacts\tests\evidence-<时间戳>\` 里。
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

## 选区、服务与编辑器

- `KeyMouse region pick --point --json` 选择一个点；`region pick --json` 选择一个矩形。返回窗口归属与坐标，并不执行点击或识别。
- `KeyMouse serve` 启动本地 Runner；用 `KeyMouse runner status` 查看状态。请求、事件、暂停、续收和终结规则见 [Runner 协议](runner-protocol.md)。
- `KeyMouse flow edit <文件>` 需要已有编辑器。Tauri 壳如何调用 CLI 见[编辑器 IPC](editor-ipc.md)。

## 暂停与继续

先运行 `KeyMouse serve`，再通过 `KeyMouse runner pause <作业号>` 暂停、`KeyMouse runner resume <作业号>` 继续。流程只会停在两步之间；当前步骤完成后、下一步发送前生效。要终止作业则用 `KeyMouse runner cancel <作业号>`。
