# KeyMouse

Windows 命令行输入模拟工具：**一条命令 = 一次真实的鼠标/键盘事件**，执行完就退出。
没有常驻进程、没有轮询、不装驱动，底层是 Win32 `SendInput`——事件进的是系统输入队列，
和应用收到的真人操作走同一条路。

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
| `mouse moveby <dx> <dy>` | 相对移动 |
| `mouse click [button] [-x X -y Y] [-n N] [-i MS]` | 点击，可先移动到指定点，`-n` 连击次数，`-i` 间隔 |
| `mouse dblclick [button] [-x X -y Y]` | 双击（等价 `click -n 2`） |
| `mouse down [button]` / `mouse up [button]` | 按住 / 松开（可做长按） |
| `mouse wheel <delta> [-x X -y Y]` | 滚轮，120 = 一格，向上为正 |
| `mouse hwheel <delta>` | 横向滚轮 |
| `mouse drag <x1> <y1> <x2> <y2> [--button B] [--steps N] [--duration MS]` | 拖拽（插值移动，模拟真人轨迹） |

`button`：`left`（默认）`right` `middle` `x1` `x2`

### 键盘

| 命令 | 说明 |
| --- | --- |
| `key press <key> [-n COUNT] [-i MS]` | 敲击，可连按 |
| `key down <key>` / `key up <key>` | 按下 / 抬起 |
| `key combo <k1+k2+...> [--hold MS]` | 组合键，如 `ctrl+shift+s`、`win+r` |
| `key type <text> [--interval MS]` | 输入文本，走 `KEYEVENTF_UNICODE`，**不依赖输入法/键盘布局**，中文直接打。默认每字 15ms，慢应用（WinUI 记事本、Electron）能跟上；`--interval 0` 最快但可能掉字符 |

按键名：`a-z` `0-9` `f1-f24` `esc` `enter` `tab` `space` `backspace` `delete` `insert`
`home` `end` `pageup` `pagedown` `up` `down` `left` `right` `ctrl` `shift` `alt` `win`
`capslock` `num0-num9` `numadd` `numsub` `nummul` `numdiv` `numdecimal` `numenter`
`printscreen` `pause`，以及 `vk:0x5B` 这种裸虚拟键逃生口。

## 例子

```powershell
KeyMouse mouse move 100 200
KeyMouse mouse click right -x 640 -y 480
KeyMouse mouse click left -n 2 -i 60          # 双击光标当前位置
KeyMouse mouse drag 400 300 900 300 --duration 600
KeyMouse mouse wheel -120 -x 1200 -y 600      # 在那个位置往下滚一格
KeyMouse key combo ctrl+shift+s
KeyMouse key type "hello 世界"
KeyMouse key press enter -n 3 -i 200
```

链式脚本（cmd / PowerShell 都行）：

```powershell
KeyMouse key combo win+r; Start-Sleep -Milliseconds 400; KeyMouse key type "notepad"; KeyMouse key press enter
```

## 退出码

| 码 | 含义 |
| --- | --- |
| 0 | 成功 |
| 1 | 运行时失败（如 `SendInput` 被拦截） |
| 2 | 参数错误（用法提示打在 stderr） |

## 设计要点

- **真事件**：`SendInput`，不是 `SetCursorPos`。光标移动走的是 `MOUSEEVENTF_MOVE |
  MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_VIRTUALDESK`，多显示器也按虚拟桌面坐标算。
- **坐标精度**：绝对坐标会被量化到 0..65535，所以移动后会回读校验，最多重试 3 次，
  实在不行才退到 `SetCursorPos` 兜底（此时就不是"事件"了，属于极少数情况）。
- **打字可靠性**：字符间隔默认 15ms，结尾再留 60ms 让目标窗口排空输入队列。实测
  "重启后立刻灌 24 字符的 WinUI 记事本"偶发丢尾字，加缓冲 + 间隔后连续 3 轮
  "中文 + 英文 + 数字" 剪贴板往返校验全部逐字符一致。目标应用特别卡时把 `--interval` 调大。
- **单次执行**：进程起来 → 注入 → 打印结果 → 退出，没有循环和后台线程。
- **落点由焦点决定**：事件发给当前前台窗口 / 光标下的窗口。要操作某个窗口，先把它切到前台——
  这和真人操作的前提完全一致。

## 已知限制

- 目标窗口若以管理员权限运行，普通权限的 KeyMouse 会被 UIPI 拦下（错误码 5，程序会提示），
  用管理员权限跑 KeyMouse 即可。
- UAC 安全桌面（如"是否允许此应用更改"弹窗）、Ctrl+Alt+Del 无法被模拟，这是系统设计如此。
- 安全软件可能把注入行为当可疑操作，按需加白名单。
