# 测试：选择能回答问题的那一层

以下命令从仓库根目录运行。默认测试不驱动真实桌面；会移动鼠标、写剪贴板或读取窗口的用例单列，不要在日常工作桌面无人值守运行。

| 想验证什么 | 命令 |
| --- | --- |
| .NET 编译、程序集依赖 | `dotnet build KeyMouse.sln -c Release` |
| 解析、录制转换、控制流、报告、协议 | `dotnet run -c Release --project tests/KeyMouse.Tests` |
| 只验证录制与回放计时 | `dotnet run -c Release --project tests/KeyMouse.Tests -- --only timeline,record-focus,schema-range` |
| 列出可单跑的模块 | `dotnet run -c Release --project tests/KeyMouse.Tests -- --list` |
| 界面类型、前端和 Rust 行为 | `pwsh tools/dev/editor-ui.ps1 check`，然后 `pwsh tools/dev/editor-ui.ps1 test` |
| 文档链接、步骤清单和版本 | `pwsh tools/dev/check-docs.ps1`，然后 `pwsh tools/dev/check-version.ps1 -NoArtifact` |

### 跨进程接口（无真实输入）

```powershell
dotnet build src/KeyMouse.Cli -c Release
$km = '.\src\KeyMouse.Cli\bin\Release\net10.0-windows\KeyMouse.exe'
python tests/KeyMouse.E2E/test_cli.py $km
python tests/mcp/test_stdio.py $km
```

[CI](../../.github/workflows/ci.yml) 还会用发布产物运行检查。不要用 `--dry-run` 的成功证明实际输入可达。对于编辑器的嵌套运行日志，使用[独立 HUD 夹具](../../tests/CallHud/README.md)；控制流测试的入口见[模块说明](../../tests/KeyMouse.Tests/FlowControl/README.md)。

### 需要解锁的测试桌面

先保存工作，关闭敏感窗口，在专用会话运行 `.\km s`。若会话不接收输入，记录为**未验证**，不要计作产品失败或通过。按用途选[冒烟测试](../tools/smoke.md)、[起录与停止](../tools/record-focus.md)或[后台控件探针](../../tests/KeyMouse.BackgroundProbe/README.md)。安装器另在干净用户目录验证安装、启动、运行示例与卸载；需要受信任签名的 UIAccess 验收独立进行。

性能与资源验证使用测试程序的 `flow-bench`、`capture-soak` 等显式入口。比较时记录构建配置、模型可用性和样本数；报告与截图存 `artifacts/tests/`，不要将真实桌面内容当作测试夹具提交。
