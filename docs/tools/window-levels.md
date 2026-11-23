# 比较执行进程与目标窗口的权限

[window-levels.ps1](../../tools/diagnostics/window-levels.ps1)用于解释后台动作的资格错误。它只用于观察，不会给目标或编辑器提权。

```powershell
pwsh tools/diagnostics/window-levels.ps1 -Filter notepad
```

先用测试进程缩小范围，再对照会话、进程身份和完整性级别。窗口看得见不代表当前进程可以向其发送输入；同级权限也不代表应用支持对应的消息。排查结论见[故障指南](../guide/troubleshooting.md)。窗口标题和进程清单可能包含隐私，不应原样附到 Issue。
