# push.ps1

一句话：把本地提交推到远端，并且**在推不动的时候说清楚卡在哪一层**。

```powershell
pwsh tools\dev\push.ps1                          # 推当前分支到 origin
pwsh tools\dev\push.ps1 -Remote upstream -Branch main
pwsh tools\dev\push.ps1 -Timeout 30              # 网络慢时放宽单次连接超时
.\km push                                        # 同一个东西的短写法
```

## 为什么单独写一个

`git push` 失败的报错长得差不多，原因却分三类，处理方式完全相反：

| 现象 | 真实原因 | 该怎么办 |
| --- | --- | --- |
| `Connection timed out` / `Could not resolve host` | 网络到不了远端 | 等网络恢复，或让 git 走代理 |
| `Permission denied (publickey)` / `access rights` | 身份不对 | `ssh -T git@github.com` 看它认为你是谁 |
| `non-fast-forward` / `rejected` | 远端有本地没有的提交 | 先 `git pull --rebase`，**别强推** |

脚本按这个顺序查一遍，只在第一种情况下说"过一会儿再来"，并且**不会**建议 `reset` 或 `--force`。

## 它做了什么

1. 读远端地址与当前分支（`-Branch` 不给就用 `HEAD`）。
2. `git fetch` 之后数一下本地领先多少个提交；相等就直接退出 0。
3. 用 `GIT_SSH_COMMAND` 带上 `ConnectTimeout`（默认 12 秒）推一次。
4. 按失败原文分类，给出上表里对应的结论。

## 参数

| 参数 | 默认 | 说明 |
| --- | --- | --- |
| `-Remote` | `origin` | 远端名 |
| `-Branch` | 当前分支 | 分支名 |
| `-Timeout` | `12` | SSH 连接超时秒数；别调太大，网络不通时等一分钟只会更难受 |

## 退出码

| 码 | 含义 |
| --- | --- |
| 0 | 已推送，或本来就没有要推的 |
| 3 | 网络到不了远端（这台机器上见过：`github.com:22` 超时、`443` 也不通） |
| 4 | 身份或权限问题 |
| 5 | 远端有新提交，需要先拉 |
| 1 | 其它失败，看原始输出 |

## 这台机器上的已知情况

2026-10-31 实测：DNS 正常、`www.baidu.com` 200，但 `github.com:443` 与 `ssh.github.com:443` 都不通；
没有代理环境变量、`ProxyEnable=0`、常见代理端口（7890/7897/10808/1080/8889/2080…）都没在听。
也就是说**推送这件事只差网络**，脚本本身没问题——网络恢复后 `.\km push` 一次就上去。

## 相关

- 发版流程：[release.md](release.md)
- 全量关卡：[verify.md](verify.md)
