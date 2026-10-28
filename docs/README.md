# KeyMouse 文档

按**读者**分四组。不知道该看哪篇时，从下面这张表里找你现在想做的事。

| 我想…… | 看这里 |
| --- | --- |
| 装上、升级、卸载 | [guide/install.md](guide/install.md) |
| 搞清楚编辑器去哪找命令行、为什么开发树里没有 `cli` 文件夹 | [guide/environments.md](guide/environments.md) |
| 用图形编辑器录一段操作 | [guide/editor.md](guide/editor.md) |
| 东西不工作了 | [guide/troubleshooting.md](guide/troubleshooting.md) |
| 查某条命令的选项与语义 | [reference/cli.md](reference/cli.md) |
| 写前端 / 接 Runner / 生成流程 JSON | [reference/interface.md](reference/interface.md)、[reference/runner-protocol.md](reference/runner-protocol.md) |
| 改代码、跑测试、发版本 | [dev/development.md](dev/development.md)、[dev/testing.md](dev/testing.md) |
| 理解为什么这样设计 | [dev/design.md](dev/design.md) |
| 用仓库里的自动化工具 | [tools/README.md](tools/README.md) |

## 四组各自的边界

- **guide/** 给使用者：装、用、坏了自己怎么查。不出现源码路径以外的实现细节。
- **reference/** 是契约：命令语义、流程 JSON 格式、前后端通信协议。改行为必须先改这里。
- **dev/** 给改代码的人：环境、流程、测试策略、设计取舍与已知限制。
- **tools/** 给天天在仓库里跑脚本的人：每个脚本一份，说清"什么时候用、怎么用、产物在哪"。

## 约定

- 文档用中文写，命令示例用 `pwsh` 代码块，路径一律相对仓库根。
- 每一篇的开头一句话要说清"这是什么、什么时候用"。
- 仓库有一道关卡会检查**相对链接是否指向真实文件**（`pwsh tools\dev\check-docs.ps1`），
  改名或移动文件时一并改链接，否则 CI 会红。
- 设计上的取舍写在 [dev/design.md](dev/design.md)，不要散落在代码注释里；代码里只留"为什么这行是这样"。
