# 后台控件探针

探针创建自己的 WinForms 靶窗和同名对照窗。成功以文本、选择区、按钮事件以及前台窗口和光标是否改变来判断，而不是只看 API 返回值。不要把外部应用或用户窗口句柄交给此程序。

在解锁的 Windows x64 专用桌面、.NET 10 SDK 和 .NET Framework 4.8 SDK 环境下运行：

```powershell
dotnet run -c Release --project tests/KeyMouse.BackgroundProbe
# 也可走正式 CLI，而不直接调用原型：
dotnet build -c Release src/KeyMouse.Cli
dotnet run -c Release --project tests/KeyMouse.BackgroundProbe -- --cli src/KeyMouse.Cli/bin/Release/net10.0-windows/KeyMouse.exe
```

运行期间不要移动鼠标或切换窗口。测试覆盖普通后台与最小化两种靶窗状态；观察遭外界干扰时不能记作通过。目标歧义、只读、密码、禁用及失效控件应被拒绝。可推广的结论与限制见[实验边界](../../docs/dev/background-input-feasibility.md)。
