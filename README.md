# KeyMouse

Windows 命令行输入模拟工具：**一条命令 = 一次真实的鼠标/键盘事件**，执行完就退出。
没有常驻进程、没有轮询、不装驱动，底层是 Win32 `SendInput`——事件进的是系统输入队列，
和应用收到的真人操作走同一条路。

v1.1 起支持**指定焦点窗口**：先验证目标窗口是否可用、再把它切到前台并**回读确认**，
确认不了就一个字节都不发。

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
| `-wx <cx> -wy <cy>` | 客户区坐标，必须成对出现且必须带选择器 |
| `--strict-point` | 额外要求该屏幕点下方的窗口就是目标本身 |

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
| 4 | 目标不可用（隐藏 / 最小化 / 被 DWM cloak / 无响应） |
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

- **不做启发式"活体检测"**（v1.2 计划）：目前的闸门都是客观判定（可见性、最小化、
  DWM cloak、消息队列是否响应）。"窗口只剩一帧画面"这类**没有通用 API 可以判定**，
  所以 v1.1 只能靠"不猜、失败要响"来兜底。
- 目标窗口若以管理员权限运行，普通权限的 KeyMouse 会被 UIPI 拦下（错误码 5，程序会提示），
  用管理员权限跑 KeyMouse 即可。
- UAC 安全桌面（"是否允许此应用更改"弹窗）、Ctrl+Alt+Del 无法模拟，系统设计如此。
- **遮挡检测做不到**：窗口被别的窗口盖住一半没有可靠 API 可查；`--strict-point`
  只能校验"某个点下面的窗口是谁"。
- 验证通过到实际注入之间有毫秒级 TOCTOU 窗口，理论上仍可能被抢焦点。
- `mouse drag` 暂不支持窗口相对坐标。

## 路线图

- **v1.2**：`--verify-change`（`PrintWindow` 前后像素比对，默认关）、
  Chromium 空壳启发式（有 `Chrome_WidgetWin_*` 类名却找不到渲染子窗口）、
  UIA 探针。

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
