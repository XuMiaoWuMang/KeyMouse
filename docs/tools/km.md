# km —— 日常开发入口

`km` 是每天都要用的那一个命令：一条命令做一件事，默认只做该做的那件。根目录的 [km.cmd](../../km.cmd) 只是薄壳，真正的实现在 [tools\dev\km.ps1](../../tools/dev/km.ps1)。

## 用法

```pwsh
.\km                  # 不带参数 = 打印脚本头部的说明
.\km b                # 只编译 CLI（Core + Runner）            ~1s
.\km ba               # 编译整个解决方案（含编辑器、安装程序）  ~18s
.\km e                # 编译编辑器，并把 CLI 发到 artifacts\cli
.\km e -NoCli         # 只编译编辑器，不碰 artifacts
.\km e -NoElevate     # 普通权限构建编辑器：不弹 UAC，也能被普通权限的 KeyMouse 点
.\km u               # 起新编辑器外壳（Tauri + Svelte）的开发模式，等价于 editor-ui.ps1 dev
.\km ub              # 打新外壳的发布版（pnpm build + cargo build --release）
.\km ut              # 跑新外壳的两层测试（vitest + cargo test）
.\km t                # 全部单元测试（不需要桌面）              ~1s
.\km t parsing flow   # 只跑指定模块（可写多个）
.\km t -n             # 不重新编译，直接跑                      ~0.2s
.\km tl               # 列出测试模块
.\km s                # 桌面冒烟（需要解锁的交互式桌面）
.\km v                # 全部关卡（= verify.ps1）
.\km env              # 环境判定：开发树还是安装版、命令行在哪
.\km docs             # 文档链接检查
.\km d                # 打包单文件到 artifacts\cli             ~5s
.\km d -s             # 打包自包含单文件到 artifacts\cli-self
.\km r key type hi    # 编译并运行刚编出来的 KeyMouse，参数原样转发
.\km r -n --help      # 不编译，直接运行
.\km w                # 监视 src/ 与 tests/：改一次跑一次"编译 + 单测"
.\km w -once          # 只跟一次（确认监视本身是好的）
.\km clean            # 清掉 bin / obj / artifacts
```

## 动词表

| 动词 | 别名 | 做什么 | 典型耗时 |
| --- | --- | --- | --- |
| `b` | `build` | 编译 `src\KeyMouse.Cli\KeyMouse.Cli.csproj`（连带 Core / Runner） | ~1s |
| `ba` | `build-all` | 编译整个 `KeyMouse.sln`（编辑器与安装程序也在里面） | ~18s |
| `e` | `editor` | 编译编辑器；除非给 `-NoCli`，否则随后打包 `artifacts\cli`；`-NoElevate` 换成不提权的清单 | — |
| `t` | `test` | 跑单元测试；可带模块名、`-n`、`-l`，或 `bench` | ~1s |
| `tl` | `test-list` | 列出测试模块（等价 `t -l`） | — |
| `s` | `smoke` | 桌面冒烟，转调 `tests\smoke\run.ps1` | 数分钟 |
| `v` | `verify` | 全部关卡，转调 `tools\dev\verify.ps1` | 数分钟 |
| `env` | `environment` | 打包（若缺）后跑 `KeyMouse doctor` | — |
| `docs` | — | 文档链接检查 | 数十毫秒 |
| `d` | `dist` | 打包单文件；`-s` / `--self-contained` 切自包含 | ~5s |
| `r` | `run` | 运行 bin 里的 KeyMouse，参数原样转发；`-n` 跳过编译 | — |
| `w` | `watch` | 轮询 `src\` 与 `tests\`，改动后跑一次"编译 + 单测" | — |
| `c` | `clean` | 删除 `src\`、`tests\` 下的 bin/obj，以及整个 `artifacts\` | — |

## 几个容易问的问题

### 为什么 `km e` 会顺带打包 `artifacts\cli`？

开发树里编辑器找的是 `artifacts\cli\KeyMouse.exe`（安装版才找自己目录下的 `cli\`，判定逻辑见 [环境判定](../guide/environments.md)）。只重建 bin 的话，编辑器里跑的仍是上次打包的旧引擎 —— 实测踩过：换行输入明明改了，编辑器里却还是旧行为。所以 `e` 默认在编译后发布一份到 `artifacts\cli`，确认不需要时用 `-NoCli` 跳过。

### 为什么 `km s` 传的是 bin 里的 exe？

`km s` 调的是 `run.ps1 -Exe src\KeyMouse.Cli\bin\Release\net10.0-windows\KeyMouse.exe`，也就是**刚编译出来的**那份，而不是 `artifacts\cli` 里的单文件 —— 后者可能是上一次打包留下的旧版本。冒烟要验的是当前代码。

### 开发树里没有 `cli\` 文件夹，正常吗？

正常。开发树的命令行在 `artifacts\cli\`，安装版才在 `<安装目录>\cli\`。`km env` 就是用来一眼确认这件事的：它跑 `KeyMouse doctor`，把判定结果和每个候选位置都列出来（实现在 `src\KeyMouse.Core\Product\ProductLayout.cs`）。

## 退出码

- 被调用的构建/测试失败时，`km` 打印 `x <标签> 失败（退出码 N）` 并**立刻以该退出码退出**。
- `t` / `tl` / `s` / `v` / `env` / `docs` / `r` 都以 `exit $LASTEXITCODE` 透传下层结果。
- 未知动词：打印脚本头部的说明段落，退出码 0（等于帮助）。

## 注意

- `-q` 只关掉 `BuildCli` 这一步：`km b -q` 于是什么都不做；`km e -q` / `km t -q` 仍有后续步骤（发布/跑测试），但不会重新编译（源码里就这么写，正常构建别带 `-q`）。
- `km t` 只把**非 `-` 开头**的参数拼成 `--only a,b`；`-l`、`-n`、`bench` 各有分支。
- `km clean` 连 `artifacts\` 一起删，之后第一次 `km s` / `km e` 需要重新打包。
- `km r` 转发的是 `bin\Release\net10.0-windows\KeyMouse.exe`；要跑单文件产物就直接调 `artifacts\cli\KeyMouse.exe`。
- 参数与行为以 [km.ps1](../../tools/dev/km.ps1) 为准；本页只写它真实存在的分支。
