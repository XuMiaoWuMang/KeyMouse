# check-docs.ps1 —— 文档链接关卡

[check-docs.ps1](../../tools/dev/check-docs.ps1) 检查仓库里每一条相对链接是否指向真实存在的文件。文档是分开写的（README、`docs/guide/`、`docs/dev/`、`docs/reference/`、`docs/tools/`），一次改名就会让另一个人几天后点进 404，而没人会回头重读 —— 所以它单独成了一道关。

## 用法

```pwsh
pwsh tools\dev\check-docs.ps1                  # 查整个仓库
pwsh tools\dev\check-docs.ps1 -Root D:\Data\KeyMouse\docs   # 只查某个目录
.\km docs                                      # 日常入口（等价，不带参数）
```

| 参数 | 默认值 | 说明 |
| --- | --- | --- |
| `-Root` | 仓库根（脚本上一级的上二级） | 从这个目录开始递归找 `*.md` |

## 查什么

1. 递归收集 `-Root` 下所有 `*.md`。
2. 跳过这些目录（它们不是文档）：`bin`、`obj`、`artifacts`、`.git`、`.vs`、`evidence`。
3. 用 `\]\(([^)\s]+)\)` 抓出每条 markdown 链接的目标，并跳过三类不归它管的目标：带协议头的（`http:`、`mailto:` 等）、以 `#` 开头的（页内锚点）、以 `//` 开头的。
4. 去掉 `#fragment` 部分，把剩下的相对路径按 `.md` 文件所在目录拼成绝对路径，`Test-Path` 判断存在性。
5. 每条断链打印一行 `broken: <md 相对路径> -> <链接原文>`，最后汇总：

```
checked <N> relative link(s) across <M> markdown file(s); broken: <K>
```

## 为什么跳过 evidence 目录

`artifacts\tests\evidence-*` 是本地取证归档，其中的 `summary.md` 会**复述 OCR 读到的东西**。OCR 读出来的文本长得跟 markdown 链接一模一样：脚本自己的注释里记着实测样例 —— 样例中被识别成「[是]（3）[取消]」的一段，写成半角括号后就成了一条指向 `3` 的断链（这一段故意用全角括号写，否则本页自己就会被拦下）。取证目录不是给人当导航用的，跳过它才能让这道关只对真文档负责（归档本身也不进 git，见 [evidence](evidence.md)）。

`bin`、`obj` 与 `artifacts` 也一并跳过，理由相同：产物目录里的文本是机器输出，不是文档。

## 退出码

| 退出码 | 含义 |
| --- | --- |
| `0` | 没有断链 |
| `1` | 至少一条断链 |

不需要桌面、不需要构建，几十毫秒跑完，所以 CI 每次 push 都跑。

## 注意

- **链接里不要有空格**：正则要求目标里没有空白字符，`[说明](a b.md)` 这种写法整条都匹配不上，等于静默不检查。
- 只解析相对路径；`https://…`、`mailto:…`、`#anchor`、`//host/path` 一律跳过，锚点是否真的存在也不校验。
- 检查的是"文件存在"，不是"内容对不对"：链接指到一个存在但已改名内容的文件，它不会拦。
- 新建或大改文档后跑一次 `.\km docs`，比等 CI 报出来快得多。
