# 推送前检查与推送失败诊断

[push.ps1](../../tools/dev/push.ps1) 会执行 `git fetch` 和 `git push`，并区分网络、身份验证、远端提交领先等情况。它不是只读检查，也不替你审查提交内容。

先运行 `git status --short`、`git diff --cached --stat` 和[公开材料检查](../guide/publication.md)。只有在已确认分支、目标仓库和要发布的提交后才运行：

```powershell
pwsh tools/dev/push.ps1 -Remote origin
```

默认使用当前分支，可用 `-Branch` 和 `-Timeout` 调整。非快进失败时先了解远端变更；不要因普通失败强推。共享错误信息时不要粘贴含凭据的远端 URL 或个人 SSH 配置。
