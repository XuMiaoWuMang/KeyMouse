# 安装与发布：0.1.0

两个产物，两条标签，各管各的。

| 产物 | 标签 | 装完长什么样 |
| --- | --- | --- |
| KeyMouse（命令行） | cli-v0.1.0 | 单文件 KeyMouse.exe，自包含或框架依赖 |
| KeyMouse 流程编辑器（GUI） | gui-v0.1.0 | 安装程序，默认装到 %LOCALAPPDATA%\Programs\KeyMouse |

## 安装程序做了什么

1. 解开编辑器（自包含的 WinUI，装完即用，不需要先装 .NET）。
2. 写注册表 GuiPath = 编辑器 exe 的绝对路径。
3. 找命令行，顺序：注册表 CliPath → 安装目录的 cli 子目录 → PATH。
   - 找到了：不覆盖，登记下来（除非这次明确只要界面）。
   - 没找到：解开自带的命令行到 <安装目录>\cli\，并写 CliPath。
4. 建开始菜单快捷方式；安装目录里放一份卸载.cmd（同时删注册表键）。

只装界面（--no-cli，或面板里取消勾选）时只写 GuiPath，一个字节都不碰 CliPath。

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
