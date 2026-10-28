# 安装、升级与卸载

这个页面只回答一件事：**怎么把 KeyMouse 放到机器上、怎么换版本、怎么拿走**。
装完怎么用，看[流程编辑器](editor.md)与[命令参考](../reference/cli.md)；东西不工作了看[故障排查](troubleshooting.md)。

当前版本 **0.2.1**（引擎 **2.1.0**）。开发树里怎么编译、怎么打包发版，是另一回事，见[开发与发布流程](../dev/development.md#版本与发布)。

## 先选文件

| 文件 | 需要预装什么 | 体积 | 适合 |
| --- | --- | --- | --- |
| `KeyMouseSetup-0.2.1.exe` | 什么都不用装 | 见发布页 | 想用图形编辑器的人（命令行顺带装上） |
| `KeyMouse-x64.exe` | 什么都不用装 | 约 49 MB | 只要命令行 |
| `KeyMouse-fx-x64.exe` | .NET 10 运行时 | 约 0.3 MB | 已经装了 .NET 10 运行时，只要能命令行 |

三者的区别只有两条：

- **要不要图形编辑器**。编辑器只在安装程序里；两个单文件都只有命令行，`KeyMouse flow edit` 打不开东西。
- **要不要自带运行时**。`KeyMouse-x64.exe` 把 .NET 运行时和 WinForms 一起塞进一个 exe，所以大；`KeyMouse-fx-x64.exe` 只有几百 KB，但机器上得先有 .NET 10 运行时，否则双击/运行时会提示缺少运行时。

自包含版那 49 MB 主要是交互式选区浮层需要的 WinForms；不用选区的命令并没有因此变重。

## 用安装程序装

双击 `KeyMouseSetup-0.2.1.exe`。安装程序是个一屏的小对话框（不做多步向导），只有四处要看：

- **安装到**：默认位置是上次装过的地方（注册表 `GuiPath` 指的那份）；从没装过才是 `%LOCALAPPDATA%\Programs\KeyMouse`。点「浏览…」可以换，但拒绝装到磁盘根目录（`C:\` 这种）。
- **一起装命令行**：首次安装时这个勾选框是灰的、强制勾上——没有命令行，编辑器跑不了流程；非首次安装时默认勾上「同时更新命令行到本安装包的版本」，取消勾选则保留机器上已有的那份命令行。
- **安装**：装完可以顺手打开编辑器。
- **取消**：什么都不做。

安装程序本身**不要求管理员**（它只写当前用户的目录和 `HKCU`）。但装出来的编辑器带 `requireAdministrator`，**启动时会弹一次 UAC**——不发一次提权，录不到、也点不动以管理员身份运行的程序。

装完的东西（`<安装目录>` 就是上面选的那个目录）：

| 位置 | 是什么 |
| --- | --- |
| `<安装目录>\KeyMouse.FlowEditor.exe` | 编辑器本体（自包含 WinUI，装完即用） |
| `<安装目录>\cli\KeyMouse.exe` | 命令行（自包含单文件，不需要另装 .NET） |
| `<安装目录>\uninstall.exe` | 卸载程序（安装程序复制自己而来） |
| 开始菜单 → `KeyMouse 流程编辑器` | 指向编辑器的快捷方式 |
| `HKCU\Software\KeyMouse` | `GuiPath`、`CliPath` 两个 REG_SZ |

关于注册表这两条，值得知道三件事：它们**只登记确实存在的文件**（登记的路径被删了就等于没登记，`doctor` 会显示「（无）」）；`GuiPath` 与 `CliPath` 可以指向不同目录（命令行允许装在别处）；编辑器找命令行时先看自己身边，再看 `<安装目录>\cli\`，最后才轮到注册表和 `PATH`——完整顺序见[开发树与安装版](environments.md)。

覆盖安装时目录怎么处理：

- 目标目录里已经有 `KeyMouse.FlowEditor.exe`（或者注册表说这个目录归 KeyMouse）→ **整体清空再解出**，旧版本文件不会残留；
- 目标是你自己的目录（比如 `D:\Tools`）→ 只删它认识的名字：`KeyMouse.*`、`cli\`、`uninstall.exe`、旧版的 `卸载.cmd`，别的东西不动。

### 静默安装

CI 或脚本里装的时候用得上：

```pwsh
.\KeyMouseSetup-0.2.1.exe --silent                                  # 装到默认位置
.\KeyMouseSetup-0.2.1.exe --silent --dir D:\Tools\KeyMouse          # 指定目录
.\KeyMouseSetup-0.2.1.exe --silent --no-cli                         # 只要编辑器，别动命令行
.\KeyMouseSetup-0.2.1.exe --print-default                           # 只打印默认目录，不装
```

退出码 `0` = 装好了，`1` = 没装成。装不成时先看 `%TEMP%\keymouse-setup.log`（开头一行是 `start <参数>`，失败那行是 `install failed: …`）；注册表写失败不拦安装，但会往同一个目录的 `keymouse-registry.log` 记一行。这两个日志在 `%TEMP%` 不可写时会落到备用目录，`KeyMouse doctor` 的「临时文件：」一行写的就是实际位置。

## 只用命令行：解压即用

两个单文件都**不需要安装**，也不需要管理员：

```pwsh
mkdir D:\Tools\KeyMouse
move .\KeyMouse-x64.exe D:\Tools\KeyMouse\
```

这样做不写注册表、不建快捷方式、不占 `%LOCALAPPDATA%`。要让 `KeyMouse` 在任何目录都能直接敲，把这个目录加进用户 `PATH`：

```pwsh
[Environment]::SetEnvironmentVariable(
    'Path',
    [Environment]::GetEnvironmentVariable('Path', 'User') + ';D:\Tools\KeyMouse',
    'User')
```

改完要**新开一个终端**才生效。也可以只给当前会话加，够用一次就好：

```pwsh
$env:Path += ';D:\Tools\KeyMouse'
```

确认拿到的是哪一份：

```pwsh
KeyMouse --version      # KeyMouse 0.2.1（引擎 2.1.0）
KeyMouse doctor         # 环境判定、自己是谁、命令行在哪、注册表里写着什么
```

便携用法的卸载就是删掉那两个文件（以及你自己加进 `PATH` 的那一项）。

## 升级

- **装了安装版的**：直接跑新版安装程序。它默认装回原目录、把目录整体换掉；命令行默认跟着更新，取消勾选就保留旧的。
- **只想换命令行**：用新版的自包含 `KeyMouse.exe` 覆盖 `<安装目录>\cli\KeyMouse.exe`（它本来就是单文件，覆盖即可），再 `KeyMouse --version` 核对。
- **便携的**：换掉那个 exe。

升级后确认两件事：`KeyMouse --version` 报的是你期望的版本；`KeyMouse doctor` 里「检查：」三条都是 `ok`。编辑器与命令行是同一个产品的两半，**版本别拆开太久**——混着用会出现「编辑器拉起了旧命令行，旧命令行不认识新参数」这类怪症状，见[故障排查](troubleshooting.md)。

## 降级

KeyMouse 没有内置的降级动作，做法就是**用旧版的文件覆盖回去**：

- 旧版安装程序直接跑一遍即可：它会清空安装目录再解出旧文件；
- 只降命令行：把旧版的单文件 exe 覆盖到 `<安装目录>\cli\KeyMouse.exe`；
- 降级不会回退 `%LOCALAPPDATA%\KeyMouse` 里的设置与 OCR 模型（`editor.json`、`tessdata\`），一般无影响；真出问题就删掉那个目录（下次启动会重建）。

## 卸载

打开安装目录，双击 `uninstall.exe`；也可以从命令行静默卸载：

```pwsh
& '<安装目录>\uninstall.exe' --silent        # 不问，直接卸
& '<安装目录>\uninstall.exe' --yes           # 有界面但不弹确认框
```

它会按顺序做这些：

1. 结束正在运行的编辑器与命令行进程；
2. 删掉安装目录（卸载程序通过一个临时脚本删自己，所以目录可能在 1–3 秒后才消失，这是正常的）；
3. 命令行如果登记在别的目录：那个目录的叶子是 `cli` 或以 `KeyMouse` 开头就整棵删，否则只删 `KeyMouse.exe` / `KeyMouse.dll` / `KeyMouse.Core.dll` / `KeyMouse.Runner.dll` 四个文件，不动别人的东西；
4. 删开始菜单里那条 `KeyMouse 流程编辑器` 快捷方式；
5. 清空 `HKCU\Software\KeyMouse` 整个键。

静默模式退出码 `0` 表示该删的都删了；`1` 表示有东西没删掉（进程赖着不走最常见），详情看 `%TEMP%\keymouse-setup.log` 末行的 `uninstall done, problems=N`。

### 卸载不会带走的残留

这些是设计上不动的（可能还有用，也可能属于别的程序），需要就手动清：

| 位置 | 是什么 |
| --- | --- |
| `%LOCALAPPDATA%\KeyMouse\editor.json` | 编辑器最近打开的 5 个流程 |
| `%LOCALAPPDATA%\KeyMouse\editor-crash.log` | 编辑器崩溃日志 |
| `%LOCALAPPDATA%\KeyMouse\tessdata\` | OCR 模型（默认从这儿找） |
| `%TEMP%\keymouse-setup.log`、`keymouse-registry.log` | 安装/注册表日志 |
| `%TEMP%\keymouse-record.json` | 编辑器录制时的临时流程 |
| `<流程名>.shots\` | 录制/回放时存的关键步骤截图 |
| 你自己写的流程 JSON | 你的文件 |

## 装完先跑这三条命令

第一次装完之后，用这三条命令确认「装对了、看得见东西、能跑流程」：

```pwsh
KeyMouse doctor                                  # 环境、版本、权限、编码、注册表、三项检查
KeyMouse window list                             # 当前有哪些可见窗口，标题与状态（别急着点，先看）
KeyMouse run samples\notepad-demo.txt --dry-run  # 示例脚本的资格检查：不发送任何输入
```

三条都过了再去掉 `--dry-run` 真跑一遍。注意 `samples\` 只在仓库里有——用安装版的话，新建一个自己的 `.txt` 脚本，或者从仓库拷一份；脚本怎么写见[命令参考](../reference/cli.md#run脚本)。
