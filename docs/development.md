# 开发与发布流程

命令与选项见 [reference.md](reference.md)，设计取舍见 [design.md](design.md)。

## 四道关卡

| 关卡 | 命令 | 需要桌面 | CI 会跑 |
| --- | --- | --- | --- |
| 构建 | `dotnet build KeyMouse.sln -c Release` | ❌ | ✅ |
| 单元测试 | `dotnet run -c Release --project tests\KeyMouse.Tests` | ❌ | ✅ |
| 文档链接检查 | `pwsh tests/check-docs.ps1` | ❌ | ✅ |
| **桌面冒烟** | `pwsh tests\smoke.ps1` | ✅ | ❌（runner 没有交互式桌面） |
| 性能基准 | `dotnet run -c Release --project tests\KeyMouse.Tests -- bench` | 部分 | ❌（按需手动跑） |

前两项是"逻辑没坏"，第三项防文档腐烂，第四项是**唯一能证明"手没抖"的关卡**。

## 改一处代码

1. 改代码；
2. `dotnet build KeyMouse.sln -c Release` —— **0 警告是底线**；
3. `dotnet run -c Release --project tests\KeyMouse.Tests` —— 纯逻辑，秒级；
4. 只要碰到 **窗口选择 / 闸门 / 注入 / 脚本执行** 任一条路径 → 跑 `pwsh tests\smoke.ps1`；
5. 提交：一个提交做一件事，消息里写清**为什么**（"改了什么"看 diff 就知道）；
6. `git push`。

`tests\smoke.ps1` 会启动 `tests\KeyMouse.SmokeTarget`（一个整块客户区都是文本框的小窗口），
把它当作靶子跑完 45 项断言，最后只结束**它自己启动的那个进程**——**不会碰你开着的任何程序**。
它会临时占用剪贴板（结束还原），失败时返回非零退出码。

> **控制台编码**：程序按**控制台自己的码页**输出（cp936 就写 GBK，65001 就写 UTF-8），
> 因为 PowerShell 正是用 `[Console]::OutputEncoding`（跟随控制台码页）解码原生命令输出的。
> 冒烟脚本开头会先做一次中文往返检查：如果这个宿主把两者设得不一致，它会**只报一条清晰错误**
> 并退出，而不是让后面十几条中文断言莫名其妙地失败。

## 发布流程

**发布不由 push 触发，由 tag 触发，而且必须等人工验证。**

1. 候选改动已经在 `main` 上、CI 绿；
2. **由人验证**：跑一遍 `pwsh tests\smoke.ps1`，再手动试一下这次改动的功能；
3. 验证通过后再打 tag：

   ```powershell
   git tag -a v1.6.0 -m "KeyMouse v1.6.0 - ..."
   git push origin v1.6.0
   ```

4. tag 触发 `.github/workflows/release.yml`：先跑单元测试，再构建两个产物，附加到 GitHub Release；
5. 收尾：确认 Release 页面两个文件都在，**下载其中一个实跑一次 `--version`**。

> 为什么不让 push 直接发布：这个工具最糟的失败模式是"输入发到了错误的窗口"，
> 而这类问题只有真桌面上跑一遍才看得出来。CI 能证明逻辑没坏，不能证明手没抖。

## CI 做什么

`.github/workflows/ci.yml`（每次 push / PR）：构建解决方案 → 单元测试 → 文档链接检查 →
做一次发布构建（保证发布命令本身没坏）。

`.github/workflows/release.yml`（推 `v*` tag）：单元测试（不过就不发）→ 构建
`KeyMouse-fx-x64.exe`（框架依赖，约 240 KB）与 `KeyMouse-x64.exe`（自包含，约 36 MB）→
附加到同名 Release 并生成 release notes。

## 加新检查时放哪里

| 检查的性质 | 放哪 |
| --- | --- |
| 纯逻辑（解析、匹配、筛选、状态串） | `tests/KeyMouse.Tests` —— 零依赖控制台程序，**不要引测试框架** |
| 需要真窗口 / 真注入 | `tests/smoke.ps1` 里新加一节，用 `Check` 断言并给出失败细节；靶子是 `tests/KeyMouse.SmokeTarget`，**不要借用用户自己的程序** |
| 需要看数字 | `ParseBench`（`-- bench`），它已经会同时报"解析"和"执行"两边的成本 |
| 文档一致性 | `tests/check-docs.ps1` |

单元测试故意不用 xunit/NUnit：一个普通控制台程序，零依赖、离线可跑、`dotnet run` 就是全部用法。

## 版本号

`KeyMouse.csproj` 的 `<Version>` 和 `Program.Version` **必须一致**（内置帮助从后者取版本）。
未发布前的多次改动共用一个版本号；发布之后才 +1。
