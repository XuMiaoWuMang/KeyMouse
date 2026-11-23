# 综合验证：理解通过和跳过的区别

[verify.ps1](../../tools/dev/verify.ps1)汇总解决方案构建、无桌面单测、文档与版本检查、示例流程、编辑器检查、打包以及桌面冒烟。运行前先看工具链和桌面是否可用。

```powershell
pwsh tools/dev/verify.ps1 -SkipSmoke -SkipPack
# 在发布用交互会话中执行全部适用检查：
pwsh tools/dev/verify.ps1
```

`-SkipSmoke` 避免真实桌面输入；`-SkipPack` 不重新生成发布件，不等于已有产物可省略。缺少工具或桌面不满足条件时应读取结论中的跳过项。需要验证全新安装仍应在干净环境中运行安装器，不要把构建成功当作安装验收。
