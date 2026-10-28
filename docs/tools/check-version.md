# check-version.ps1 —— 版本关卡

[check-version.ps1](../../tools/dev/check-version.ps1) 保证版本号只有一个来源、每条版本线各归其位、打包出来的产物自报的版本也跟源码一致。改过 [Directory.Build.props](../../Directory.Build.props) 或任一 csproj 之后跑它。

## 用法

```pwsh
pwsh tools\dev\check-version.ps1                # 连产物一起查
pwsh tools\dev\check-version.ps1 -NoArtifact    # 还没打包时只查源码
```

| 参数 | 默认值 | 说明 |
| --- | --- | --- |
| `-Root` | 仓库根 | 从这里定位 `Directory.Build.props` 与 `src\` |
| `-NoArtifact` | 关 | 跳过第 4 项（`artifacts\cli\KeyMouse.exe` 自报版本） |

## 单一来源与两条版本线

唯一来源是根目录 [Directory.Build.props](../../Directory.Build.props)：

| 版本线 | 当前值 | 谁是这条线的产品 |
| --- | --- | --- |
| `ProductVersion` | `0.2.1` | `KeyMouse.Cli`、`KeyMouse.FlowEditor`、`KeyMouse.Setup` |
| `EngineVersion` | `2.1.0` | `KeyMouse.Core`、`KeyMouse.Runner` |

其余地方一律引用 `$(ProductVersion)` / `$(EngineVersion)`，运行时代码从程序集读，不再出现字面量。产品版本是用户看到的那个，"引擎"是 Core/Runner 的能力版本，两条线各自演进。

## 查四件事

1. **来源本身**：`Directory.Build.props` 里两条版本线都在，且都是 `x.y.z`（正则 `^\d+\.\d+\.\d+$`）。文件缺失直接以 1 退出。
2. **谁该用哪条线**：逐个核对下面五个 csproj 里写的是 `<Version>$(对应版本)</Version>`，写错或写平均报错。

   | 项目 | 应当引用 |
   | --- | --- |
   | `src\KeyMouse.Cli\KeyMouse.Cli.csproj` | `$(ProductVersion)` |
   | `src\KeyMouse.FlowEditor\KeyMouse.FlowEditor.csproj` | `$(ProductVersion)` |
   | `src\KeyMouse.Setup\KeyMouse.Setup.csproj` | `$(ProductVersion)` |
   | `src\KeyMouse.Core\KeyMouse.Core.csproj` | `$(EngineVersion)` |
   | `src\KeyMouse.Runner\KeyMouse.Runner.csproj` | `$(EngineVersion)` |

3. **没有内联字面量**：`src\` 下所有 `*.csproj` / `*.props` / `*.targets` 里不许出现 `<(ProductVersion|EngineVersion|Version)>\s*\d+\.\d+`；`src\` 下所有 `*.cs` 里不许出现 `const string Version = "<数字>…"`。
4. **产物自报版本**（不带 `-NoArtifact` 时）：跑 `artifacts\cli\KeyMouse.exe --version`，输出必须同时包含 `ProductVersion` 与 `EngineVersion`；产物不存在时只打印一行"跳过产物版本检查"，不算失败。

## 输出与退出码

通过时逐行 `ok …`，结尾：

```
版本关卡：通过（产品 0.2.1 / 引擎 2.1.0）
```

任何一项不过就逐行 `x …`（带文件与行号），结尾 `版本关卡：没过（N 项）`，退出码 `1`。通过退出码为 `0`。

## 它防的是哪几种错

- **csproj 里写死版本**：改 props 忘了改 csproj，装出来的程序标题是旧版本号。
- **`.cs` 里写死常量**：运行时从程序集读版本，代码里再留一份就必然不同步。
- **两条线混用**：Core 引用了 `$(ProductVersion)`，引擎版本从此跟产品版本绑死。
- **产物版本对不上**：`artifacts\cli\KeyMouse.exe` 是上一次打包的旧产物 —— 这正是"改完没重新打包就发出去"的现场。

## 注意

- 只扫 `src\`：`tests\`、`tools\` 下的脚本里出现版本字符串不在检查范围。
- `verify.ps1` 调它时**不带** `-NoArtifact`，所以在没有产物的干净树上只会看到跳过提示；想明确"只看源码"，手跑时加 `-NoArtifact`。
- 版本号改了之后，发版前记得整条链都过一遍：见 [verify](verify.md) 与 [发布](release.md)。
