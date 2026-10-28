# 自动化工具总览

仓库里所有自动化脚本的索引：每个工具做什么、什么时候该用它、详细说明在哪一篇。改完代码先在这里找入口，别手敲 `dotnet publish` —— 那样产出的东西不落 `artifacts\`，也不受关卡保护。

## 总览

| 工具 | 位置 | 一句话作用 | 什么时候用 |
| --- | --- | --- | --- |
| `km` | `tools\dev\km.ps1`（根入口 [km.cmd](../../km.cmd)） | 日常开发入口，一条命令做一件事 | 每天：编译、单测、冒烟、打包、监视 |
| `build.ps1` | `tools\build\build.ps1` | 把 CLI 打成单文件，落 `artifacts\cli` 或 `artifacts\cli-self` | 要单文件产物，或 CI 要复现同一条发布命令 |
| `pack-release.ps1` | `tools\build\pack-release.ps1` | 打一次完整发布：编辑器 + 两个命令行 + 安装程序 | 发版前，产出 `artifacts\release\` 三件套 |
| `verify.ps1` | `tools\dev\verify.ps1` | 串起六道关卡，最后给一个明确结论 | 每次对外交付前 |
| `check-docs.ps1` | `tools\dev\check-docs.ps1` | 校验仓库里每条相对链接都指向真实文件 | 动过文档；CI 每次 push 都跑 |
| `check-version.ps1` | `tools\dev\check-version.ps1` | 确认版本号只有一个来源，且产物与源码一致 | 改过版本号、发版前；CI 每次 push 都跑 |
| `run.ps1`（冒烟） | `tests\smoke\run.ps1` | 在真实桌面上让 KeyMouse 往靶子窗口打字再对账 | 改过输入、焦点、选区、录制、Runner、流程 |
| `check-input.ps1` | `tools\dev\check-input.ps1` | 一句话实验：这个会话收不收得到注入的输入 | 冒烟忽然红一片、换机器、怀疑注入被系统丢掉 |
| `evidence.ps1` | `tools\diagnostics\evidence.ps1` | 归档读屏取证：每次调用的 JSON、截图、耗时 | 怀疑读屏不稳，需要拿数据说话 |
| `window-levels.ps1` | `tools\diagnostics\window-levels.ps1` | 列出窗口所属进程的权限级别 | 点了没反应/没记录，怀疑 UIPI 拦截 |
| `screenshot.ps1` | `tools\diagnostics\screenshot.ps1` | 把指定窗口或整屏截成 PNG | 改过界面，要用眼睛验收、要留现场 |
| `release.ps1` | `tools\release\release.ps1` | 建/撤销 GitHub Release 并上传产物 | 打标签之后真正发版 |
| `release-notes.ps1` | `tools\release\release-notes.ps1` | 按 git 历史生成发版说明 | 需要发版正文，或想预览这次改了什么 |
| `clean-releases.ps1` | `tools\release\clean-releases.ps1` | 清掉遗留的 Release 与旧命名标签 | 命名规则变更后收拾旧账 |

## 日常

- [`km`](km.md) —— 除了下面这些专用脚本，其他事都从它开始。
- [桌面冒烟 `tests\smoke\run.ps1`](smoke.md) —— 需要解锁的交互式桌面；`km s` 就是对它的包装。
- [`check-input.ps1`](check-input.md) —— 冒烟之前先问一句"这个会话收不收得到注入的输入"。
- [`verify.ps1`](verify.md) —— 把构建、单测、文档、版本、打包、冒烟串成一次运行。
- [`check-docs.ps1`](check-docs.md) —— 文档改动后跑一次，几十毫秒的事。
- [`check-version.ps1`](check-version.md) —— 动了 `Directory.Build.props` 或任一 csproj 之后跑。

## 构建

- [`build.ps1` 与 `pack-release.ps1`](build.md) —— 单文件产物、编辑器发布目录、安装程序载荷。

## 发布

- [`release.ps1` / `release-notes.ps1` / `clean-releases.ps1`](release.md) —— 两条发布线、标签与版本的对应关系、`GITHUB_TOKEN` 的使用。

## 诊断

- [`evidence.ps1`](evidence.md) —— 读屏取证的本地归档。
- [`window-levels.ps1`](window-levels.md) —— 窗口权限级别与 UIPI。
- [`screenshot.ps1`](screenshot.md) —— 窗口/整屏截图，产物落 `artifacts\shots\`；改完界面用它验收。

## 共同约定

- 脚本都按 PowerShell 7 写，用 `pwsh -NoProfile -File <脚本>` 调用；`km.cmd` 只是 `tools\dev\km.ps1` 的薄壳。
- 失败用非 0 退出码或 `throw` 表达；需要连续跑完的关卡（`verify.ps1`）会收集失败项再统一退出。
- 所有产物只落 `artifacts\`（已 gitignore）：`cli`、`cli-self`、`editor`、`setup`、`release`、`tests`。旧的 `dist\` 与 `out\` 已废弃。
- 版本号唯一来源是根目录 [Directory.Build.props](../../Directory.Build.props)：产品 0.2.1、引擎 2.1.0。
- 本文档里的相对链接会被 [`check-docs.ps1`](check-docs.md) 校验 —— 增删链接后跑一次。

## 相关

- 目录结构、开发树的运行方式：[开发指南](../dev/development.md)
- 开发树与安装版的产物位置：[环境判定](../guide/environments.md)
- CLI 的命令与退出码：[命令行参考](../reference/cli.md)
