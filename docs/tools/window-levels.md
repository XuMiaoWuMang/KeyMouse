# window-levels.ps1 —— 窗口权限级别

[tools\diagnostics\window-levels.ps1](../../tools/diagnostics/window-levels.ps1) 列出每个可见窗口所属进程的权限级别。排查"点了没记录 / 打了字没反应"时先跑它：Windows 的 UIPI 只允许同级或更高级完整性的进程向目标发送输入，普通权限的 KeyMouse 往提权程序里点，事件会被系统丢掉而且**不报错**。

## 用法

```pwsh
.\tools\window-levels.ps1                 # 全部可见窗口
.\tools\window-levels.ps1 -Filter bili    # 标题或进程名含 bili 的
.\tools\window-levels.ps1 -Under          # 光标底下那个窗口（先把鼠标停到目标上再跑）
```

| 参数 | 默认值 | 说明 |
| --- | --- | --- |
| `-Filter` | 空（全部） | 在窗口标题或进程名里按不区分大小写匹配 |
| `-Under` | 关 | 只看光标底下那个顶层窗口，并额外打印类名 |

## 输出

默认列表三列，标题超过 52 字符截断：

```
进程               权限     标题
explorer           普通     C:\Windows\explorer.exe
ApplicationFrameHost 普通   设置
Notepad            已提权   未命名 - 记事本
```

`-Under` 打单个窗口，格式不同：

```
光标下窗口：
  进程  notepad（pid 4242）
  权限  已提权
  类名  Notepad
  标题  未命名 - 记事本
```

权限只有三种取值：

| 级别 | 含义 |
| --- | --- |
| 普通 | 与当前进程同级（或更低），能接收 KeyMouse 的输入 |
| 已提权 | 目标以管理员身份运行，普通权限的注入会被 UIPI 拦下 |
| 读不到 | 连进程令牌都打不开（隔离更强，例如某些系统进程）或进程已退出 |

## 什么时候用

- 用户说"点了没记录"：先看目标是 `已提权` 还是 `普通`。提权目标需要 KeyMouse 也以管理员身份启动。
- 筛目标窗口时找进程名 / 标题写法，方便 `--find`、`--title` 之类的参数。
- 确认"前台窗口到底是哪个进程"——比如锁屏判定里出现的 `LockApp`、`LogonUI`。

## 注意

- 输出固定按 UTF-8 写：中文列名在 GBK 控制台里会变成问号，所以脚本自己切了 `[Console]::OutputEncoding`。
- `-Under` 取的是该点所属的**顶层**窗口（`GetAncestor` 到根），不一定是最终接收鼠标消息的子控件；看权限足够了，看具体控件用 `window inspect`。
- 需要交互式桌面（要枚举真实窗口），不需要管理员权限；看不到某个窗口通常是它不可见或属于别的会话。
- 脚本内嵌的枚举代码编译成类 `KLevelV2`：同一个 PowerShell 会话里重复运行不会撞名，改那段 C# 时把类名版本号 +1。
- 它只观察、不修改；成功执行时退出码为 0，脚本本身没有额外的失败分支。
