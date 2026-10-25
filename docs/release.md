# 安装与发布：0.1.0

两个产物，两条标签，各管各的。

| 产物 | 标签 | 装完长什么样 |
| --- | --- | --- |
| KeyMouse（命令行） | cli-v0.1.0 | 单文件 KeyMouse.exe，自包含或框架依赖 |
| KeyMouse 流程编辑器（GUI） | gui-v0.1.0 | 安装程序，默认装到 %LOCALAPPDATA%\Programs\KeyMouse |

## 安装程序做了什么

1. **默认安装位置 = 上次装过的地方**（注册表 GuiPath 所在目录）；没装过才用 `%LOCALAPPDATA%\Programs\KeyMouse`。
2. **完全替换**：目标目录如果属于 KeyMouse，先整体清空再解开，旧版本文件不会残留；
   如果目录不是我们的（用户自己选的文件夹），只删已知文件名，不动别人的东西。
3. 解开编辑器（自包含的 WinUI，装完即用，不需要先装 .NET）。
4. 命令行：
   - **首次安装**（注册表里没有可用的 GuiPath）：**强制**装进 `<安装目录>\cli\`——没有 CLI 编辑器跑不了流程；
   - **非首次**：面板上给"是否更新命令行"的勾选，**默认勾上**（本次安装包的版本），取消则保留已有的。
   注册表里登记的路径**只认确实存在的文件**：指向已被清掉的临时目录时会被忽略并清除。
5. 写注册表 GuiPath / CliPath，建开始菜单快捷方式。
6. 把**自己复制成 `<安装目录>\uninstall.exe`**（一个二进制两个身份：改名或带 `--uninstall` 即卸载）。

## 卸载

双击 `<安装目录>\uninstall.exe`（或带 `--silent` 静默），它会：

- 结束正在运行的编辑器与命令行；
- 删除编辑器目录；命令行若登记在别处，只删 `KeyMouse*.dll/exe` 这几个文件，不整棵删别人的目录；
- 删除开始菜单快捷方式；
- **清空** `HKCU\Software\KeyMouse`；
- 最后用临时脚本删掉自己（因为它就跑在要删的目录里）。

静默模式下退出码 0 表示该删的都删了；目录会在 1-3 秒内消失（自删脚本等进程退出）。

## 为什么用注册表

发布环境里 GUI 与 CLI 可能装在不同目录（换盘、只装其中一个、命令行早就在别处）。
「命令行就在我旁边」这个猜测在发布环境里错过一次：装完之后编辑器找不到引擎，播放直接失败。
现在两边只共享一个约定：

```
HKCU\Software\KeyMouse
    GuiPath    REG_SZ    <安装目录>\KeyMouse.FlowEditor.exe
    CliPath    REG_SZ    <某处>\KeyMouse.exe
```

编辑器读它，安装程序写它；代码里再没有硬编码的安装布局（相对路径只作为开发目录的兜底）。

## 打包

```
.\tools\pack-release.ps1            # 编辑器 + 命令行 + 安装程序，产物在 dist\
.\tools\pack-release.ps1 -SkipCli    # 只重打编辑器与安装程序
```

一个坑：dotnet publish 不会把 WinUI 的 KeyMouse.FlowEditor.pri 带进输出目录，
缺它编辑器启动即 XamlParseException。打包脚本从 Release 构建目录补这个文件。

## 撤销与重新发布

```
$env:GITHUB_TOKEN = 'ghp_...'
.\tools\release.ps1 -RevokeAll    # 删掉所有 Release 与标签
.\tools\release.ps1 -Publish      # 按 0.1.0 重新发布
```

没有 token 时只能删标签（git push origin --delete <tag> 走 SSH 就够），Release 对象删不掉。
