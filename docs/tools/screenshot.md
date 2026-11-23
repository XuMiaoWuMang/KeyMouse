# 截取可分享的测试窗口

[screenshot.ps1](../../tools/diagnostics/screenshot.ps1)按窗口标题、进程或 ID 取图；`-List` 仅用于本机确认候选窗口。用专用测试窗口和虚构文字，不要默认截整屏。

```powershell
pwsh tools/diagnostics/screenshot.ps1 -List
pwsh tools/diagnostics/screenshot.ps1 -Process KeyMouse.SmokeTarget -Client
```

默认位置是 `artifacts/shots/`，可用 `-Out` 指定文件。`-Client` 只取客户区；`-PrintWindow` 请求窗口自身绘制，应用未必支持。文件名和像素都可能暴露窗口标题。公开前要检查二者，而非仅按扩展名过滤。
