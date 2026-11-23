# 验证产品版本与引擎版本

版本源是 [Directory.Build.props](../../Directory.Build.props)，不要从文档或旧安装包猜测版本。[检查脚本](../../tools/dev/check-version.ps1)验证版本格式、项目引用与已构建 CLI 的自报版本。

```powershell
pwsh tools/dev/check-version.ps1 -NoArtifact  # 仅检查源码
pwsh tools/dev/check-version.ps1              # 同时检查发布产物
```

前者用于干净检出，后者用于发布前。若只有产物检查失败，先重新构建对应输出，不要把源码版本改成旧二进制显示的值。
