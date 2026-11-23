# 验证录制没有自动插入聚焦动作

[record-focus.ps1](../../tools/diagnostics/record-focus.ps1)分别从桌面和普通靶窗开始录制，再检查写出的第一步和实际回放。不应出现不存在于用户操作中的 `focus`，也不应给桌面输入绑定一个无关进程。

```powershell
pwsh tools/diagnostics/record-focus.ps1
```

这不是无桌面测试：需要可用 CLI、已编译的 `KeyMouse.SmokeTarget` 及解锁的专用桌面。可用 `-Exe`、`-TargetExe` 指定路径；`-DurationMs` 等超时参数只控制一轮测试。结果、流程与报告写入 `artifacts/tests/` 的独立目录。普通 CI 仅运行 `dotnet run -c Release --project tests/KeyMouse.Tests -- --only record-focus`，不要把二者当作同一覆盖。
