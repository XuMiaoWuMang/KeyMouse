# screenshot.ps1：把窗口截成 PNG

`tools\diagnostics\screenshot.ps1`

改过界面就得用眼睛验收，而且要能事后比较："这版和上一版差在哪儿"。光凭描述判断排版，
结论往往和实际差得远——所以这个脚本把窗口抓成图片，产物落在 `artifacts\shots\` 下，按时间分目录。

## 什么时候用

- 改过编辑器界面（XAML、间距、控件顺序），想确认实际渲染结果。
- 报了个"按钮挤在一起"的问题，需要把现场留下来。
- 需要把某个窗口的某块区域放大看（截图后裁切放大，比盯着屏幕猜快）。

不需要它的时候别用：想要的是**界面里的文字**，用 `KeyMouse probe`（读屏）比截图更省事。

## 怎么用

```powershell
# 先看窗口到底叫什么
pwsh tools\diagnostics\screenshot.ps1 -List

# 按标题通配截（默认会先把窗口提到前台）
pwsh tools\diagnostics\screenshot.ps1 -Title '*流程编辑器*'

# 只截内容区（去掉标题栏与边框）——检查布局时用这个
pwsh tools\diagnostics\screenshot.ps1 -Process KeyMouse.FlowEditor -Client

# 指定输出位置
pwsh tools\diagnostics\screenshot.ps1 -Title '*编辑器*' -Out artifacts\shots\before.png

# 整屏
pwsh tools\diagnostics\screenshot.ps1 -FullScreen
```

| 参数 | 作用 |
| --- | --- |
| `-Title` | 标题通配符，默认 `*`。`-CaseSensitive` 区分大小写 |
| `-Process` | 进程名（不带 `.exe`），和 `-Title` 一起用时两个条件都要满足 |
| `-Id` | pid。给自动化脚本用，比标题稳 |
| `-Client` | 只截客户区 |
| `-PrintWindow` | 让窗口自己重绘（窗口被遮住时用）。WinUI 窗口经常只截到空白，别指望它 |
| `-FullScreen` | 整屏（含所有显示器） |
| `-Out` | 输出路径；相对路径按仓库根解析 |
| `-DelayMs` | 提到前台后等多久再截，默认 350 ms。动画还没结束就加大 |
| `-List` | 只列可见窗口 |

## 产物与退出码

- 默认路径：`artifacts\shots\<yyyyMMdd-HHmmss>\<窗口标题>.png`，`-Client` 时文件名带 `-client`。
- `0` 成功；`4` 没找到匹配窗口（会把当前可见窗口列出来）；`5` 窗口矩形不可用（大概率最小化了）。

截图**不进 git**：`artifacts\` 整体被忽略。需要留证据的结论请写进文档或提交说明，别指望图片。

## 已知限制

- 默认截法是"截屏幕上那块区域"，所以**别的窗口盖住它就会盖进图里**；脚本会先尝试把目标窗口提到前台，
  但如果你的窗口管理器不让它抢前台，图里就会有别人的窗口。
- `-PrintWindow` 对 WinUI 3 / 分层组合的窗口经常是空白——这不是脚本坏了，是那类窗口不响应 `WM_PRINT`。
- 高 DPI 下脚本会先把自己设成 Per-Monitor V2，否则矩形是逻辑像素，图会偏一块。

## 相关

- [window-levels.ps1](window-levels.md)：先确认窗口属于哪个进程、什么权限，再决定截谁。
- [evidence.ps1](evidence.md)：要的是"读屏结果 + 截图 + JSON"的成套归档，用它而不是手动截。
