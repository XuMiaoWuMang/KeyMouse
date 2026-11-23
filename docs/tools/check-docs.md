# 检查文档链接与流程步骤表

运行 `pwsh tools/dev/check-docs.ps1`。这项离线检查无需编译或解锁桌面，适用于文档改名、项目移动或修改流程契约后。

[脚本](../../tools/dev/check-docs.ps1)检查 Markdown 的仓库相对链接，并把[步骤参数表](../reference/interface.md)与源 schema 的步骤名和循环默认值对照。非零退出时按输出修改来源页；不要为了让链接变绿而提交截图或临时报告。

它不验证外站可达性、截图内容、命令行为或密钥。提交范围另按[公开材料检查](../guide/publication.md)复核。
