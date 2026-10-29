# 构建与打包

[build.ps1](../../tools/build/build.ps1) 把 CLI 打成一个单文件；[pack-release.ps1](../../tools/build/pack-release.ps1) 打一整套发布产物（编辑器 + 两个命令行 + 安装程序）。日常用 `km d` 就够了，这两个脚本是它的底层，也是 CI 在没有 `km` 的环境里复现同一条命令的入口。

## build.ps1

```pwsh
pwsh tools\build\build.ps1                  # 框架依赖 → artifacts\cli\KeyMouse.exe
pwsh tools\build\build.ps1 -SelfContained   # 自包含   → artifacts\cli-self\KeyMouse.exe
```

| 参数 | 默认值 | 说明 |
| --- | --- | --- |
| `-Configuration` | `Release` | 传给 `dotnet publish -c` |
| `-RuntimeIdentifier` | `win-x64` | 传给 `dotnet publish -r` |
| `-SelfContained` | 关 | 打到 `artifacts\cli-self`，且加 `-p:EnableCompressionInSingleFile=true` |

它执行的就是这一条发布命令（框架依赖版）：

```
dotnet publish src\KeyMouse.Cli\KeyMouse.Cli.csproj -c Release -r win-x64
  --self-contained:false -p:PublishSingleFile=true --nologo -o artifacts\cli
```

自包含时 `--self-contained:true`，并额外压缩单文件 —— 注释里写明这是为了和 `.github/workflows/release.yml` 对齐，否则本地包会比发布包大一圈。

成功打印 `OK -> <exe 完整路径>`，`dotnet publish` 失败则 `throw`（退出码非 0）。产物只有一个 `KeyMouse.exe`。

## pack-release.ps1

```pwsh
pwsh tools\build\pack-release.ps1                 # 全打
pwsh tools\build\pack-release.ps1 -SkipEditor     # 只重打命令行与安装程序
pwsh tools\build\pack-release.ps1 -SkipCli        # 编辑器 + 安装程序
```

| 参数 | 默认值 | 说明 |
| --- | --- | --- |
| `-SkipEditor` | 关 | 跳过两个编辑器的发布（老壳 publish + 新壳 `tauri build`）；但语言目录清理与 `.pri` 补齐不受它管，只要 `artifacts\editor` 已存在就照样执行 |
| `-SkipCli` | 关 | 跳过两个命令行产物 |

版本号不在这里写死：从 [Directory.Build.props](../../Directory.Build.props) 读 `ProductVersion`，读不到直接 `throw`。执行顺序：

1. 发布编辑器（老壳，WinUI）：`--self-contained true -p:WindowsAppSDKSelfContained=true` → `artifacts\editor`。
2. 发布编辑器（新壳，Tauri）：在 `src\KeyMouse.EditorUI` 里 `pnpm tauri build --no-bundle`，把 `src-tauri\target\release\KeyMouse.Editor.exe`（约 3 MB，界面已内嵌）拷进 `artifacts\editor-ui`。
3. 清理 `artifacts\editor`：删掉用不到的语言目录、`*.pdb`、`createdump.exe`。
4. 从 Release 构建目录把 `*.pri` 复制进 `artifacts\editor`。
5. 发布命令行两次：自包含单文件 → `artifacts\cli-self`；框架依赖单文件 → `artifacts\cli`。
6. 复制成最终名字：`KeyMouse-x64.exe`（自包含）、`KeyMouse-fx-x64.exe`（框架依赖）。
7. 把两份编辑器压成 `editorui.zip`（单个 exe）、`flowedit.zip`（目录），把自包含命令行压成单条目 `cli.zip`，都落 `src\KeyMouse.Setup\Assets\`（这三个 zip 已 gitignore）。
8. 发布安装程序（自包含单文件）→ `KeyMouseSetup.exe` → 复制成 `KeyMouseSetup-<版本>.exe`。最后打印 `artifacts\release\` 的文件表。

产物一览：

| 目录 | 内容 |
| --- | --- |
| `artifacts\editor\` | 老壳（WinUI）自包含发布目录 |
| `artifacts\editor-ui\` | 新壳（Tauri）单个 exe |
| `artifacts\cli\` | 框架依赖单文件（开发树里编辑器用的就是它） |
| `artifacts\cli-self\` | 自包含单文件 |
| `artifacts\setup\` | 安装程序发布目录 |
| `artifacts\release\` | 最终三件：`KeyMouseSetup-<版本>.exe`、`KeyMouse-x64.exe`、`KeyMouse-fx-x64.exe` |

老壳为什么还要单独补 `.pri`：`dotnet publish` 不会把 WinUI 的 `KeyMouse.FlowEditor.pri` 带进输出目录，缺了它一启动就 `XamlParseException`（实测）。新壳没有这个问题——它只有一个 exe。

## 自包含与框架依赖

| | 框架依赖 | 自包含 |
| --- | --- | --- |
| 目标机器需要 | .NET 10 桌面运行时 | 什么都不用装 |
| 体积 | 小（单个 exe） | 大（运行时打进 exe，压缩后仍明显更大） |
| 谁在用 | `artifacts\cli`：开发树编辑器调用它 | `KeyMouse-x64.exe` 发布件、安装程序、编辑器 |

两个命令行产物都发给用户：装机量不足的机器用自包含，已有运行时的机器用框架依赖版。

## 编辑器为什么要自包含

WinUI 3 依赖 Windows App Runtime，而它在一台没装过运行时的机器上装不起来：bootstrapper 对 inbox CBS 运行时返回 `0x80670016`。所以编辑器发布用 `--self-contained true -p:WindowsAppSDKSelfContained=true`，把运行时整套带上 —— 代价是 269 MB 的编辑器官网包。

## `.pri` 必须手动补

`dotnet publish` **不会**把 `KeyMouse.FlowEditor.pri` 带进输出目录，缺了它编辑器一启动就 `XamlParseException`（实测）。脚本从同一份 Release 构建里补：

```
src\KeyMouse.FlowEditor\bin\Release\net10.0-windows10.0.26100.0\win-x64\*.pri  →  artifacts\editor\
```

因此打包前要先有 Release 构建（`km e` 或 `km ba`）；`bin` 里没有这个目录时脚本只是静默跳过，产物能打出来但装到机器上起不来。

## 语言目录只能删不能搬

为了让根目录瘦身，很容易想到"把 96 个语言目录挪进 `language\`"——不行：WinUI 的 MRT 只认 exe 旁边的文化目录，搬进子目录后进程启动即崩（实测 `0xC000027B` / `InvalidCastException`；对照组"原样"与"只换目录"都能起来）。能做的只有删：保留 `zh-CN`、`en-US`、`en-us`，其余匹配 `^[a-zA-Z]{2,3}(-[A-Za-z0-9]+)*$` 的目录一律删（界面文案是中文硬编码，框架消息回退英文；删掉约 11.9 MB 资源，不影响启动）。

## 注意

- `pack-release.ps1` 不构建解决方案，编辑器/命令行的编译由各自的 `dotnet publish` 完成；`.pri` 仍依赖 `bin\Release\...` 里已有的构建。
- 往 `artifacts\release\` 复制成品那两步在 `-SkipCli` 的判断之外：用它跳过命令行发布时，`artifacts\cli` 与 `artifacts\cli-self` 里必须已经有上一次的 exe，否则复制会失败。
- 两个脚本都不读 git 状态、不打标签：发版流程见 [发布](release.md)。
- 想只验证"单文件能不能起来"，用 [`verify.ps1`](verify.md) 的打包关卡（它调的就是 `build.ps1`）。
