# 发布：两条发布线与三个脚本

[tools\release\](../../tools/release/release.ps1) 下三个脚本负责 GitHub Release：建与撤销（`release.ps1`）、生成发版正文（`release-notes.ps1`）、清理遗留对象（`clean-releases.ps1`）。它们**都不构建任何东西** —— 产物由 [pack-release.ps1](build.md) 先打好，落在 `artifacts\release\`。

## 两条发布线

| 标签 | 内容 | 对应产物 |
| --- | --- | --- |
| `gui-v<版本>` | 编辑器 + 安装程序 | `KeyMouseSetup-<版本>.exe` |
| `cli-v<版本>` | 两个命令行 | `KeyMouse-x64.exe`、`KeyMouse-fx-x64.exe` |

两条线各自独立演进：`cli-*` 只跟上一个 `cli-*` 比，`gui-*` 只跟上一个 `gui-*` 比。标签里的版本号必须与 [Directory.Build.props](../../Directory.Build.props) 的 `ProductVersion` 一致 —— CI 在推标签时会比对 `GITHUB_REF_NAME`，对不上直接失败。

## 标准流程

```pwsh
.\km v                                                          # 关卡全绿
pwsh tools\build\pack-release.ps1                               # 产物进 artifacts\release
$env:GITHUB_TOKEN = ''                                          # Contents: write
pwsh tools\release\release.ps1 -Publish -DryRun                 # 先看要发什么
pwsh tools\release\release.ps1 -Publish
```

打标签（由作者手动做）：

```pwsh
git tag gui-v0.2.1; git tag cli-v0.2.1; git push origin gui-v0.2.1 cli-v0.2.1
```

## release.ps1

```pwsh
pwsh tools\release\release.ps1 -Publish -DryRun   # 只打印要做什么（不需要 token）
pwsh tools\release\release.ps1 -Publish
pwsh tools\release\release.ps1 -RevokeAll
```

| 参数 | 默认值 | 说明 |
| --- | --- | --- |
| `-RevokeAll` | 关 | 删掉仓库里所有 Release，再删本地与远端的旧标签 |
| `-Publish` | 关 | 按当前版本建 `gui-v*` 与 `cli-v*` 两个 Release 并上传产物 |
| `-DryRun` | 关 | 只打印（列出会被删或被上传的文件，缺文件标 `缺文件`） |
| `-Repository` | `XuMiaoWuMang/KeyMouse` | 目标仓库 |

- 版本号从 `Directory.Build.props` 读；读不到就 `throw`。没有 `GITHUB_TOKEN`（且不是 `-DryRun`）同样 `throw`。
- 正文来自 `release-notes.ps1 -Tag <gui-v版本>`，写进临时文件再读回来；两个 Release 用同一份正文。
- 上传按 `api.github.com` 建 Release、`uploads.github.com` 传文件；某个产物不存在时打印 `缺文件，跳过：…` 并继续。
- `-RevokeAll` 没有 `-DryRun` 时会真的删：先逐个 `DELETE /releases/<id>`，再 `git tag -d` 删本地标签、`git push origin --delete` 删远端标签。**它不管标签命名，会把 `git tag` 列出的全都删掉。**
- 两个开关都不给时脚本什么都不做（不会报错）。

## release-notes.ps1

```pwsh
pwsh tools\release\release-notes.ps1                              # 预览（对比上一个同族标签）
pwsh tools\release\release-notes.ps1 -Tag cli-v0.2.1 -Out notes.md
pwsh tools\release\release-notes.ps1 -Range "cli-v0.2.0..HEAD"
```

| 参数 | 默认值 | 说明 |
| --- | --- | --- |
| `-Tag` | 最新的标签（`git describe --tags --abbrev=0`） | 只用来判定族（`cli-*` / `gui-*`）与比较区间 |
| `-Range` | 上一个同族标签 `..HEAD` | 手动指定 git 区间，给了就不用上面的推导 |
| `-Out` | 空（打到标准输出） | 写到文件 |
| `-Max` | `60` | 逐条列出的上限，超出的折叠成一行提示 |

没有上一个同族标签时退化为 `HEAD~N..HEAD`（`N` 取 `min(25, 提交数-1)`）。提交按前缀归档：

| 前缀 | 标签 | 前缀 | 标签 |
| --- | --- | --- | --- |
| `feat` | 新增 | `refactor` | 重构 |
| `fix` | 修复 | `build` | 构建 |
| `perf` | 优化 | `docs` | 文档 |
| `test` | 测试 | `chore` | 其它 |

每条提交一行，前缀换成中文标签、短 sha 用反引号包住，形如：

```
- **新增**（cli）：支持 --find  `a1b2c3d`
```

顶部是 `## 本次更新（N 项）`，末尾附安装说明与 `区间：…`。取 `git log --no-merges`，合并提交不单独成条；不符合 `type(scope): 摘要` 的提交原样列成一行，前缀不在上表里的类型直接跳过；一条都没有时写"本次只有内部整理，没有面向用户的变更"。

## clean-releases.ps1

```pwsh
pwsh tools\release\clean-releases.ps1                        # 只列出（不删）
pwsh tools\release\clean-releases.ps1 -Remove gui,v0.1.0
pwsh tools\release\clean-releases.ps1 -Remove gui,v0.1.0 -Yes # 跳过确认
```

| 参数 | 说明 |
| --- | --- |
| `-Remove` | 要删的标签名（逗号分隔），同时删 Release 与标签 |
| `-Yes` | 不询问，直接删 |

token 取 `$env:GITHUB_TOKEN`，没有就读 `%USERPROFILE%\.keymouse-github-token`（一行纯 token，**脚本用完会删掉这个文件**），都没有就 `throw`。不给 `-Remove` 时只打印现有的 Release 与标签，是安全的。

## 注意

- 发版不由 push 触发普通构建：推的是标签，CI 才会跑；`verify.ps1` 结尾也提醒"全绿之后告诉作者，由他打 tag 发布"。
- 顺序不能反：Release 建好之后再补传文件会变成"先建空 Release、再想办法塞文件"，v0.2.0 第一次推标签时就踩过这个坑（编辑那步失败，上传被跳过，Release 空着）。要么用 `release.ps1 -Publish` 一次做完，要么让 CI 用发布动作一次性上传。
- `release.ps1` 不检查 `artifacts\release\` 是否新鲜；先跑 `pack-release.ps1`，必要时核对文件时间。
- 三个脚本都没写 `#requires`，用 `pwsh` 直接跑即可；失败通过 `throw` 表达（`pwsh -File` 下退出码为 1）。
