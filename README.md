# KeyMouse

[![ci](https://github.com/XuMiaoWuMang/KeyMouse/actions/workflows/ci.yml/badge.svg)](https://github.com/XuMiaoWuMang/KeyMouse/actions/workflows/ci.yml)

Windows 命令行输入模拟工具：**一条命令 = 一次真实的鼠标/键盘事件**，执行完就退出。
没有常驻进程、没有轮询、不装驱动，底层是 Win32 `SendInput`——事件进的是系统输入队列，
和应用收到的真人操作走同一条路。

v1.1 起支持**指定焦点窗口**：先验证目标窗口是否可用、再把它切到前台并**回读确认**，
确认不了就一个字节都不发。

v1.2 起支持**脚本批量执行**：把多条命令写进一个文件（或从 stdin 灌入），
`KeyMouse run script.txt` 顺序执行，每条命令复用同一套选择器、闸门与焦点验证。

## 下载

不想编译的话，去 [Releases](https://github.com/XuMiaoWuMang/KeyMouse/releases) 拿现成的：

| 文件 | 需要什么 | 体积 |
| --- | --- | --- |
| `KeyMouse-x64.exe` | 什么都不用装 | 约 36 MB |
| `KeyMouse-fx-x64.exe` | 需要 .NET 10 运行时 | 约 220 KB |

两个都是单文件，扔到 PATH 里就能直接 `KeyMouse ...` 用。

## 构建

```powershell
cd D:\Data\KeyMouse
.\build.ps1                 # 需要 .NET 运行时；产物 dist\KeyMouse.exe
.\build.ps1 -SelfContained  # 打包运行时，拷到别的机器也能跑
```

想全局用，把 `dist` 加进 PATH：

```powershell
[Environment]::SetEnvironmentVariable('Path', $env:Path + ';D:\Data\KeyMouse\dist', 'User')
```

## 测试

```powershell
dotnet run -c Release --project tests\KeyMouse.Tests\KeyMouse.Tests.csproj   # 纯逻辑，不需要桌面
pwsh tests\smoke.ps1 -Exe dist\KeyMouse.exe                                  # 需要交互式桌面
```

- **单元测试**（零依赖，`tests/KeyMouse.Tests`）：脚本分词器、argv/全局选项解析、
  按键名映射、选择器匹配、候选筛选与状态串。**不需要桌面，CI 里跑的就是它。**
- **桌面冒烟**（`tests/smoke.ps1`）：自己起一个记事本，走完整链路打字并用剪贴板回读
  逐字符比对，另含退出码断言与 `--dry-run` 零输入验证。会临时占用剪贴板（结束会还原）。

CI（`.github/workflows/ci.yml`）在每次 push 时构建解决方案并跑单元测试；
推 `v*` tag 时 `.github/workflows/release.yml` 会跑测试、构建两个产物并挂到 Release。

## 用法

```
KeyMouse <group> <command> [参数] [选项]
```

### 鼠标

| 命令 | 说明 |
| --- | --- |
| `mouse pos` | 打印当前光标坐标 |
| `mouse move <x> <y>` | 移动到绝对像素坐标 |
| `mouse move -wx <cx> -wy <cy> <选择器>` | 移动到某窗口客户区的点 |
| `mouse moveby <dx> <dy>` | 相对移动 |
| `mouse click [button] [-x X -y Y] [-n N] [-i MS]` | 点击，可先移动，`-n` 连击，`-i` 间隔 |
| `mouse dblclick [button] [-x X -y Y]` | 双击 |
| `mouse down [button]` / `mouse up [button]` | 按住 / 松开 |
| `mouse wheel <delta> [-x X -y Y]` | 滚轮，120 = 一格，向上为正 |
| `mouse hwheel <delta>` | 横向滚轮 |
| `mouse drag <x1> <y1> <x2> <y2> [--button B] [--steps N] [--duration MS]` | 拖拽（插值轨迹） |
| `mouse drag -wx <cx1> -wy <cy1> --wx2 <cx2> --wy2 <cy2> <选择器>` | 拖拽两个**客户区**点之间 |

`button`：`left`（默认）`right` `middle` `x1` `x2`

### 键盘

| 命令 | 说明 |
| --- | --- |
| `key press <key> [-n COUNT] [-i MS]` | 敲击，可连按 |
| `key down <key>` / `key up <key>` | 按下 / 抬起 |
| `key combo <k1+k2+...> [--hold MS]` | 组合键，如 `ctrl+shift+s`、`win+r` |
| `key type <text> [--interval MS]` | 输入文本，走 `KEYEVENTF_UNICODE`，**不依赖输入法/键盘布局**，中文直接打。默认每字 15ms |

按键名：`a-z` `0-9` `f1-f24` `esc` `enter` `tab` `space` `backspace` `delete` `insert`
`home` `end` `pageup` `pagedown` `up` `down` `left` `right` `ctrl` `shift` `alt` `win`
`capslock` `num0-num9` `numadd` `numsub` `nummul` `numdiv` `numdecimal` `numenter`
`printscreen` `pause`，以及 `vk:0x5B` 这种裸虚拟键逃生口。

### 窗口（v1.1）

| 命令 | 说明 |
| --- | --- |
| `window list [--filter <串>] [--process <名>] [--all]` | 列出顶层窗口及其状态（默认只列可见有标题的） |
| `window inspect <选择器>` | 逐条说明这个窗口为什么可用/不可用 |
| `window focus <选择器>` | 只做聚焦 + 验证，不发送任何输入 |

**选择器**（可组合，多个条件之间是 AND）：

| 选项 | 说明 |
| --- | --- |
| `--title <子串>` | 标题子串，大小写不敏感 |
| `--title-exact <文本>` | 标题完全匹配 |
| `--class <类名>` | 窗口类名 |
| `--process <exe 名>` | 进程名，如 `notepad`、`QQ` |
| `--pid <n>` | 进程 ID |
| `--hwnd <0x1234\|1234>` | 窗口句柄 |
| `--pick <n>` | 多个可用候选时选第 n 个（1-based，Z 序从上到下） |

**策略选项**：

| 选项 | 说明 |
| --- | --- |
| `--focus-policy gentle`（默认） | `SetForegroundWindow` → 回读 `GetForegroundWindow` 验证 → 最多 3 次 |
| `--focus-policy none` | 目标必须已经是前台，否则拒绝 |
| `--focus-attempts <n>` | 温和模式的尝试次数（默认 3） |
| `--allow-restore` | 允许自动还原最小化窗口（默认关） |
| `-wx <cx> -wy <cy>` | 客户区坐标，必须成对出现且必须带选择器；`mouse drag` 里它是**起点**，终点用 `--wx2 <cx2> --wy2 <cy2>`（四个必须都给） |
| `--strict-point` | 额外要求该屏幕点下方的窗口就是目标本身 |

### 脚本（v1.2，v1.3 补强）

```
KeyMouse run <文件|-> [--delay MS] [--keep-going] [--dry-run] [--echo]
                       [--retry N] [--retry-delay MS] [--set name=value] [--report file.json]
```

一行一条命令，语法和命令行**完全一致**——没有第二套语言要学，选择器和闸门逐行生效：

```text
# 注释和空行会被跳过
sleep 400
mouse click left -wx 200 -wy 200 --process notepad
key type "hello 中文也可以" --process notepad
key press enter --process notepad
```

| 选项 | 说明 |
| --- | --- |
| `--delay MS` | 每条命令之间等这么久（默认 0） |
| `--keep-going` | 出错也继续跑完（默认首错即停） |
| `--dry-run` | 只做解析与资格检查，**一个字节都不发**，也不抢焦点 |
| `--echo` | 连子命令自己的输出也打出来 |
| `--retry N` | 单条命令最多额外重试 N 次（**只对"确定没发出任何输入"的失败重试**） |
| `--retry-delay MS` | 重试前等多久（默认 300） |
| `--set name=value` | 定义变量，脚本里用 `${name}` 引用（可重复给） |
| `--report file.json` | 写一份机器可读的执行报告 |

**安全重试**是这里的重点：只有退出码 **3/4/5**（选择器没匹配、目标不可用、焦点验证失败）
才会重试——这三类失败**构造上保证一个字节都没发出去**，所以重试绝不会重复执行一个"做了一半"的动作
（比如"点了按钮但还没输入完"）。退出码 1/2 永不重试。

**变量**在**分词之后**替换，所以带空格的值仍然是**一个参数**：

```text
key type "${text}" --process ${app}
```
```powershell
KeyMouse run demo.txt --set app=notepad --set "text=你好 世界"
```

未定义的变量（哪怕一次 `--set` 都没给）会**带行号报错**，不会把 `${x}` 原样漏给下游命令。

**报告**长这样（中文不会被转义成 `\uXXXX`）：

```json
{
  "script": "D:\\demo.txt",
  "total": 3,
  "succeeded": 2,
  "failed": 1,
  "retriedCommands": 1,
  "injectedEvents": 14,
  "exitCode": 3,
  "stoppedAtLine": 3,
  "commands": [
    { "index": 1, "line": 2, "command": "key type \"hi\" --process notepad",
      "exitCode": 0, "attempts": 1, "durationMs": 412, "injectedEvents": 4, "output": "typed 2 chars" }
  ]
}
```

`injectedEvents` 是**真实注入到系统输入队列的事件数**，`--dry-run` 时它全程为 0 —— 报告本身就是
"到底有没有发东西"的证据。

**等待与断言**（v1.4）——这是"我怎么知道动作真的生效了"的答案：

```text
mouse click left -wx 200 -wy 300 --process notepad
waitfor --title "另存为" --timeout 2000     # 没弹出保存对话框就在 2 秒后失败（exit 3）
waitgone --title "正在加载" --timeout 5000   # 等它消失
```

- `waitfor <selector>`：等到**有**一个通过闸门的窗口匹配（默认 5000ms 超时、200ms 间隔）
- `waitgone <selector>`：等到**没有**匹配的窗口
- `--timeout 0` = 立刻断言，不等
- 超时 → `exit 3`（属于"确定没发出输入"的失败，所以 `--retry` 对它也安全）
- `--dry-run` 下**不会真的等**：只检查一次当前状态并报告"本来会等多久"

其它规则：
- `sleep <ms>` 是内置伪命令，用来等界面反应。
- 行内 `"引号"` 把空格包成一个参数；`\` 可转义 `"` 和 `\`；`#` 之后是注释；
  每行可以用 `--` 显式结束选项解析。
- 脚本文件必须是 **UTF-8**：ANSI/GBK 会明确报错，不会静默变成乱码。
- 退出码 = 第一条失败命令的退出码；`--dry-run` 全通过则返回 0。
- 脚本里不能再 `run` 另一个脚本（拒绝嵌套）。
- 样例见 `samples/notepad-demo.txt`。

## 例子

```powershell
# 绝对坐标（v1 行为，完全不变）
KeyMouse mouse move 100 200
KeyMouse mouse click right -x 640 -y 480
KeyMouse key combo ctrl+shift+s

# 指定窗口：先验证、再聚焦、再操作
KeyMouse key type "hello 世界" --process notepad
KeyMouse mouse click left -wx 120 -wy 340 --title "记事本"
KeyMouse window list --process QQ
KeyMouse window inspect --title "记事本"
KeyMouse window focus --hwnd 0x2079C

# 多候选时显式指定（否则报错并列出候选）
KeyMouse window focus --process explorer --pick 3
```

## 退出码

| 码 | 含义 |
| --- | --- |
| 0 | 成功 |
| 1 | 运行时失败（如 `SendInput` 被 UIPI 拦截） |
| 2 | 参数错误 |
| 3 | 选择器无匹配 / 多候选未 `--pick` |
| 4 | 目标不可用（隐藏 / 最小化 / 被 DWM cloak / 无响应 / **被禁用**） |
| 5 | **焦点验证失败——未发送任何输入** |

## 设计要点

- **真事件**：`SendInput`，不是 `SetCursorPos`。光标移动走 `MOUSEEVENTF_MOVE |
  MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_VIRTUALDESK`，多显示器按虚拟桌面算。
- **坐标精度**：绝对坐标会被量化到 0..65535，移动后回读校验、最多重试 3 次，
  实在不行才退到 `SetCursorPos` 兜底。
- **fail-closed**：指定了窗口就必须验证通过才动手。选择器歧义、窗口不合格、
  抢不到前台——统统拒绝执行并返回对应退出码，绝不"猜一个窗口往里敲"。
- **优先可见窗口**：`--process QQ` 这种查询会同时匹配到 IME、WorkerW 之类的隐藏辅助窗口，
  所以匹配后优先只看可见的那些；只有全都不可见时才回退去看隐藏的，
  这样报错信息才指向真正的那个人话窗口（退出码 4 而不是 3）。
- **打字可靠性**：字符间隔默认 15ms，结尾再留 60ms 排空队列。实测过 WinUI 记事本
  "连灌 24 字符丢尾字"的偶发竞态，加缓冲后连续 3 轮剪贴板往返校验逐字符一致。
- **DPI 感知**：`app.manifest` 声明 Per-Monitor V2。缺了它，缩放非 100% 的显示器上
  系统会虚拟化坐标，`ClientToScreen` 算出来的点会整体偏移。
- **单次执行**：进程起来 → 注入 → 打印结果 → 退出，没有循环和后台线程。

## 已知限制

- **不做启发式"活体检测"——这是调研后的决定，不是遗漏。** 详见下一节。
- 目标窗口若以管理员权限运行，普通权限的 KeyMouse 会被 UIPI 拦下（错误码 5，程序会提示），
  用管理员权限跑 KeyMouse 即可。
- UAC 安全桌面（"是否允许此应用更改"弹窗）、Ctrl+Alt+Del 无法模拟，系统设计如此。
- **遮挡检测做不到**：窗口被别的窗口盖住一半没有可靠 API 可查；`--strict-point`
  只能校验"某个点下面的窗口是谁"。
- 验证通过到实际注入之间有毫秒级 TOCTOU 窗口，理论上仍可能被抢焦点。

- **客户区坐标包含"窗口装饰"**：现代应用（WinUI/Electron）的客户区里有标签栏、工具栏。
  实测记事本客户区 y≈80 是工具栏，点那里之后**应用会吞掉你接着输入的第一个词**
  （`hello from…` 变成 `from…`，`A B C…` 变成 `B C…`）——这是应用自身行为，真人这么点也一样。
  用 `-wx/-wy` 时请瞄准内容区（记事本 y≥160 就没问题），不确定就先 `window inspect`
  或截图确认落点。

## 为什么不做"这个窗口是不是只剩一帧"的启发式检测

起因是一个真实案例：某个 Electron 应用把窗口缩进托盘后，`ShowWindow` 能把它"拉"出来，
但拉出来的只是**上一帧画面**（渲染子窗口已经没了），点它、打字全都没反应。问题是：
**能不能自动识别这种窗口？** 调研后决定：**不做**，并且这个决定有数据支撑。

在本机 16 个可见窗口上实测（2026-10-02）：

| 候选信号 | 实测结果 |
| --- | --- |
| "Chromium 类名但找不到渲染子窗口 = 空壳" | **3 个 Chromium 窗口里 2 个踩中，其中包括当时活得好好的 DSH 窗口 → 误报率 100%**，直接否决 |
| `GetGUIThreadInfo` 的 caret 位置 | 16 个窗口全都没有 caret（它本来就是一瞬一瞬的），抓不到 |
| `IsWindowEnabled` / `WS_DISABLED` | 全部 enabled —— 信号真实（模态对话框会禁用 owner）但罕见，**已加入闸门** |
| UIA `WindowInteractionState`（`NotResponding` / `BlockedByModalWindow`） | 理论上最对症，但需要 COM 互操作或额外依赖，而且**手上没有已知的"壳窗口"可以验证它** |

结论：**与其用没有验证过的启发式去猜，不如让脚本自己声明期望**。
`waitfor` / `waitgone` + 既有的分级退出码，把"动作到底有没有生效"变成**可断言的确定事实**——
这比任何猜测都可靠，代价也更低。

万一以后真遇到"输入发出去了但什么都没发生"的场景，排查顺序建议是：
先 `window inspect` 看闸门怎么说 → 再用 `waitfor` 表达你期望的结果 →
实在需要像素级证据时，`PrintWindow` 前后比对（`--verify-change`）仍是备选，
但它**只能当报告用**：点活窗口的空白处画面同样不会变，假阴性无法避免。

## 路线图

- 暂无排期。候选项：`--verify-change`（像素比对报告）、`mouse drag` 的窗口相对坐标、
  录制成脚本（`record`，产物应为窗口相对坐标形式才可重放）。

## 许可证

**GNU Affero General Public License v3.0（AGPL-3.0）**，完整条款见 [LICENSE](LICENSE)。

Copyright (C) 2026 XuMiaoWuMang

要点（不是法律意见，以 LICENSE 原文为准）：

- 可以自由使用、修改、再分发，包括商用；
- **分发修改版时，必须同样以 AGPL-3.0 开源全部源码**；
- **如果把修改版做成网络服务提供给用户，也必须向这些用户提供源码**——这是 AGPL 比 GPL 多出来的那一条；
- 不提供任何担保。

KeyMouse 是个本地命令行工具，"网络服务"那一条平时不会触发；但如果你把它改造成在线服务，
或者集成进闭源产品再分发，AGPL 的传染性会真实生效——那时需要换协议的话，请先替换 `LICENSE`。
