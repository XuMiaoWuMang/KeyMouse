# 在隔离桌面运行冒烟测试

[入口脚本](../../tests/smoke/run.ps1)运行真实 CLI 和受控靶窗，从窗口读回效果。它验证输入是否真正抵达目标，不能替换为 dry-run，也不适合锁屏会话。

```powershell
.\km s
```

请先保存工作并使用解锁的测试会话；不确定会话能否接收输入时先做[输入可达性检查](check-input.md)。结果按轮写入 `artifacts/tests/smoke-*/`。跳过、超时和断言失败应分别记录。若需添加模块，放在 `tests/smoke/modules/`，并使用测试创建的目标，不匹配任意用户窗口。
