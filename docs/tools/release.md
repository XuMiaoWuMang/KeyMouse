# 发布维护：构建附件与创建 Release 是两件事

[版本源](../../Directory.Build.props)和[发布工作流](../../.github/workflows/release.yml)决定标签对应的附件。`cli-v<版本>` 发布两种 x64 CLI；`gui-v<版本>` 发布包含编辑器的安装器。普通文档更新不需要打标签或上传二进制。

1. 运行[版本检查](check-version.md)、[综合验证](verify.md)与[公开材料检查](../guide/publication.md)。
2. 用[打包脚本](build.md)生成候选件。分别核对安装器和独立 CLI，确认没有模型、证书或附件串包。
3. 在授权发布前只预览：`pwsh tools/release/release.ps1 -Publish -DryRun`。确认仓库、标签、附件名和版本后，再由维护者选择标签工作流或显式发布。

[Release notes 脚本](../../tools/release/release-notes.ps1)可按标签或区间生成说明；先预览，不要把提交正文里的个人信息带入公告。`release.ps1 -RevokeAll` 和 `clean-releases.ps1 -Remove` 涉及删除远端数据，**不属于常规发布步骤**。令牌由环境或 CI 的秘密存储提供，不写入示例或日志。
