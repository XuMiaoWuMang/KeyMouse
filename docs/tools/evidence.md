# evidence.ps1 —— 读屏取证归档

[tools\diagnostics\evidence.ps1](../../tools/diagnostics/evidence.ps1) 把读屏能力的测量结果整套归档：每条命令的命令行、退出码、stdout/stderr，每次调用的 JSON，每张截图，以及一张汇总表。它回答的是"读屏到底有多稳，稳在哪、不稳在哪"，而不是"这次过没过"。

## 什么时候用

- 怀疑读屏不稳：偶发读不到、读错字、深色底上读反、中文英文判定飘。
- 改过截图 / OCR / 坐标换算的链路，想在改前改后拿同一份数据对比。
- 要给别人看证据，而不是口述"有时候会失败"。

日常冒烟回答"功能还对不对"，它回答"感知质量的数字是多少"。两者不互相替代。

## 用法

```pwsh
pwsh tools\diagnostics\evidence.ps1                       # 默认 20 个样例 × 最多 3 次尝试
pwsh tools\diagnostics\evidence.ps1 -Samples 20 -Attempts 3
pwsh tools\diagnostics\evidence.ps1 -Exe artifacts\cli\KeyMouse.exe
```

| 参数 | 默认值 | 说明 |
| --- | --- | --- |
| `-Exe` | `artifacts\cli\KeyMouse.exe` | 被测的命令行 |
| `-TargetExe` | `tests\KeyMouse.SmokeTarget\bin\Release\net10.0-windows\KeyMouse.SmokeTarget.exe` | 靶子窗口 |
| `-Samples` | `20` | 采样文本条数（脚本自带一组固定样例） |
| `-Attempts` | `3` | 每次读的最多尝试次数 |

它只驱动测试自己的冒烟靶子，不碰用户开着的任何程序。需要解锁的交互式桌面（要真的把窗口摆到前台截屏）。

## 一次运行做什么

在 5 组探针配置上，对每条采样文本重复"读一次、必要时重试"：

1. **放大采样**：`Samples` 条文本 × 5 组配置 × 最多 `Attempts` 次，记录首次是否成功、重试几次、耗时、置信度。
2. **插入符**：光标读到的位置与文本是否一致。
3. **中英文判定**：分别用 `eng`、`chi_sim`、`eng+sim` 三种识别语言跑，看谁决定"这段是中文"。
4. **深色底**：浅字深底原样读 vs 归一化后再读。
5. **选区浮层**：覆盖层上的文字能不能被读到。
6. **坐标空间**：几套坐标换算的哈希是否稳定（换算一变就会换哈希）。
7. **标题栏**：标题栏区域的读取结果。

重试语义是"调用方会怎么重试"：退出码 `6` 表示"没看到"（两次抓帧不一致，通常是抓到了重绘中间态），工具文档把 3/4/5/6 列为可安全重试，所以汇总会同时给出**首次结果**与**重试后结果**。任何一次尝试的 JSON 与截图都不丢。

## 产物结构

每次运行在 `artifacts\tests\evidence-<yyyyMMdd-HHmmss>\` 下开一个新目录：

```
commands.txt   每条命令行 + 退出码 + stdout + stderr
json/          每次调用的原始 JSON；失败的那次另有 .err.txt
images/        每张截图，BMP 转 PNG 便于查看
tmp/           本次运行的 %TEMP%：probe 的中间位图、`--keep-image` 的 BMP 都落在这里
summary.md     数字与表格，按主题分节（Upscale sampling / Caret / Who decides Chinese vs English /
               Dark background / Region picker / Coordinate spaces / Title bar read / Files）
```

结束时打印归档路径 `证据已归档: <run 目录>`；成功退出码为 0（脚本没有显式的失败退出分支，命令本身失败体现在 summary 的计数里）。

## 为什么不进 git

- `.gitignore` 覆盖整个 `artifacts\`，归档天然被忽略。
- 一次 20 样例的运行约 10 MB 截图；仓库是给代码和文档用的，不该为一次取证长胖。
- 归档里是**本机某次测量**的快照：换机器、换字体渲染、换 DPI 就不可比。它属于工作区里的临时证据，需要给人看时单独发文件。

## 注意

- 截图与 JSON 全保留，所以磁盘增长比冒烟快；不需要时直接删 `artifacts\tests\evidence-*`。
- `summary.md` 会复述 OCR 读到的文本，那里面可能出现形如 markdown 链接的片段 —— 这正是 [`check-docs.ps1`](check-docs.md) 跳过 `evidence` 目录的原因。
- 归档内容以脚本为准；本页只描述它实际写出的文件与段落。
