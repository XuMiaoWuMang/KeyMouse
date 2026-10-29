# 开发树与安装版

KeyMouse 有两个入口（编辑器、命令行），而它们**在开发树里和在装好的机器上不在同一个地方**。
这不是随手定的：安装版要把命令行收进 `<安装目录>\cli\`，好让一个目录就是一份完整产品；
开发树又必须让"刚编出来的那份"优先，否则你会对着一个改了代码却没生效的编辑器发呆。

这个页面只说一件事：**哪份文件在哪、程序是怎么找到它的**。

## 一张对照表

| | 开发树（git 检出的仓库） | 安装版（`KeyMouseSetup-<版本>.exe` 装出来） |
| --- | --- | --- |
| 编辑器（新壳，Tauri） | `src\KeyMouse.EditorUI\src-tauri\target\release\KeyMouse.Editor.exe`（`pnpm tauri build --no-bundle` 或 `pnpm tauri dev`） | `<安装目录>\KeyMouse.Editor.exe` |
| 编辑器（老壳，WinUI） | `src\KeyMouse.FlowEditor\bin\Release\net10.0-windows10.0.26100.0\win-x64\KeyMouse.FlowEditor.exe` | 过渡期保留 `<安装目录>\KeyMouse.FlowEditor.exe` |
| 命令行 | `artifacts\cli\KeyMouse.exe`（`tools\build\build.ps1` 或 `km d` 的产物） | `<安装目录>\cli\KeyMouse.exe` |
| 版本号来源 | 根目录 `Directory.Build.props` | 同左（编进 exe 里） |
| 注册表 | 一般不写 | `HKCU\Software\KeyMouse`：`GuiPath`、`CliPath` |
| 权限 | 新壳**永不提权**；老壳走清单（build 阶段就编进 exe），只调界面时用 `km e -NoElevate` | 新壳 `asInvoker`、老壳 `requireAdministrator`、命令行 `asInvoker` |
| 有没有 `cli` 文件夹 | **没有**，这是正常的 | 有 |

开发树里没有 `cli\` 是设计的结果，不是缺东西：编辑器在开发时会去找 `artifacts\cli\KeyMouse.exe`。
脚本 `tools\build\build.ps1` 存在的意义就是把这个文件造出来。

## 程序怎么找对方

只有一个实现：`src\KeyMouse.Core\Product\ProductLayout.cs`。编辑器（`KeyMouseBridge.ResolveTool`）、
`KeyMouse flow edit`（`FlowEditorCommand`）都调它，谁都不许再各写一份搜索顺序——
以前就是各写一份，两份很快分了叉，`flow edit` 只认一个谁都不存在的 `edit\` 子目录。

判定"我在哪种形态里"用的是很朴素的办法：从 exe 所在目录往上走，找到同时有
`KeyMouse.sln` 与 `Directory.Build.props` 的目录，就是仓库根；找到了就是开发树，找不到就是安装版。

**命令行的候选位置**（从上到下，第一个存在的胜出）：

1. 环境变量 `KEYMOUSE_CLI`（排障、测试用：想让编辑器用哪个就指哪个）
2. `<仓库>\artifacts\cli\KeyMouse.exe` —— 开发树的正牌产物
3. `<仓库>\src\KeyMouse.Cli\bin\Release\net10.0-windows\KeyMouse.exe` —— `dotnet build` 的直接产物
4. 同一个 Debug 目录
5. 自己所在目录
6. `<自己所在目录>\cli\KeyMouse.exe` —— 安装版的布局
7. 注册表 `CliPath`
8. `PATH`

**编辑器的候选位置**：`KEYMOUSE_EDITOR` → 自己所在目录 → 上一级目录（从 `cli\` 回头看）→
开发树的新壳 `target\release` / `target\debug` → 老壳的 `bin\Release\…\win-x64` / `bin\Debug` →
注册表 `GuiPath` → `PATH`。同一个位置里**先找 `KeyMouse.Editor.exe`（新壳）、再找
`KeyMouse.FlowEditor.exe`（老壳）**——过渡期两套并存，退役那天只要把老名字从
`ProductLayout.EditorNames` 里删掉。

顺序里"离自己最近的在前、注册表与 PATH 在最后"是刻意的：开发时注册表里往往写着上一次装的旧版本，
如果让它优先，新编辑器会拉起旧命令行，旧命令行不认识新参数，报出来的错和编辑器本身毫无关系——
`--parent` 那次就是这么炸的。

## 权限：两边都提权，开发时可豁免

编辑器的 `app.manifest` 里写着 `requireAdministrator`，而清单在 **build 阶段**就并进 exe 了——
所以"开发版不提权"是个流传过的误解，实际两条路都是提权的（`window-levels.ps1` 一眼能看出来）。
命令行相反，始终是 `asInvoker`，需要时显式 `--elevate`：它会被脚本和 CI 大量调用，每次弹 UAC 等于废掉它。

开发时确实有需要普通权限的场合：**提权窗口不让普通权限的进程点它**（UIPI），
想用 `KeyMouse` 或 UIA 去验证编辑器界面，双方必须同级。这条路由开关给，不靠分叉清单：

```powershell
pwsh tools\dev\km.ps1 e -NoElevate -NoCli   # 编一份普通权限的编辑器
```

标题栏会显示「管理员」或「普通权限」，看见的是哪一份、什么时候编的，一目了然。

## 两条命令看清现状

```powershell
KeyMouse doctor     # 环境判定、每个候选位置、最后选中谁、版本、权限、编码、注册表
.\km env            # 同一个命令，顺带确保 artifacts\cli 是刚打出来的
```

`doctor` 的输出里，每个候选都会写明"来自"哪个约定；找不到时会把**找过的所有地方**列出来。
这类问题最怕只给一句"找不到文件"，那等于让人从头猜。

## 混用会出现的症状

| 症状 | 实际发生了什么 |
| --- | --- |
| 编辑器里改了引擎代码却不生效 | 编辑器读的是 `artifacts\cli\KeyMouse.exe`，`dotnet build` 只更新了 `bin\`。跑一次 `km d`（或 `km e`，它默认会跟着打包）。 |
| 编辑器播放报"不认识 --parent" | 编辑器拉起了注册表里那份旧命令行。`doctor` 看"命令行"一行指向哪儿，必要时设 `KEYMOUSE_CLI`。 |
| `KeyMouse flow edit` 说找不到编辑器 | 装的版本太老（老版本没有这个命令的目录约定），或者开发树还没编译编辑器：`km ba`。 |
| 装完编辑器一切正常，`KeyMouse` 却不能全局用 | 安装程序只把命令行放进 `<安装目录>\cli\`，不保证进 `PATH`；把那个目录加进 `PATH`，或用完整路径。 |

## 改代码时的约定

- 任何"找一个文件/一个目录"的新代码，**先看 `ProductLayout` 有没有现成的**；没有就往那里加，
  不要在调用方拼路径。
- 新增一个形态（比如将来的便携版）时，只改 `ProductLayout` 一处，并同时更新本页的对照表。
- 目录形态变了要同步三处：`ProductLayout`、本页、[`../tools/km.md`](../tools/km.md) 里 `env` 的说明。

相关：[安装与升级](install.md)、[故障排查](troubleshooting.md)、[开发流程](../dev/development.md)。
