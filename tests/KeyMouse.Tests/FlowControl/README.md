# 流程控制测试

本组不要求活动窗口：输入叶子由测试 dispatcher 接收，表达式谓词使用受控实现。`FlowRun.cs` 把临时 JSON 送入真正的加载器和执行器，最后检查报告，不依赖控制台的展示措辞。

```powershell
dotnet run -c Release --project tests/KeyMouse.Tests -- --only expr,flow-control
```

新增语法首先覆盖加载拒绝，再覆盖真假分支、循环上限、作用域与步骤来源。尤其要区分 `continue` 是否执行 `for` 的步进、`return` 是否越过子流程边界。与桌面状态有关的问题转到[隔离测试](../../../docs/dev/testing.md)，而不是在这里模拟用户桌面。
