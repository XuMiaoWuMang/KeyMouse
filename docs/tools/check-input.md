# 验证测试会话能否接收输入

[check-input.ps1](../../tools/dev/check-input.ps1) 启动受控靶窗、发送测试文本，再通过剪贴板验证实际收到的内容。它回答的是「这个会话能测吗」，不是「任意应用都能自动化吗」。

```powershell
pwsh tools/dev/check-input.ps1
```

需解锁的交互桌面和已构建的 CLI、靶窗。可用 `-Exe`、`-TargetExe` 指定文件，`-KeepOpen` 保留靶窗观察。执行中会临时使用剪贴板；不要在使用剪贴板传递敏感信息时运行。若未收到输入，先解决会话环境再跑[冒烟](smoke.md)。
