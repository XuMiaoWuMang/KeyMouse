# KeyMouse evidence 20261003-171722

Command: `pwsh tests/evidence.ps1 -Samples 20 -Attempts 3`
Machine: DESKTOP-MF86BEL, PowerShell 7.6.6

## Upscale sampling

Text is typed into the smoke target, read back from region 0,0,400,32 and compared character for character.
A read that exits 6 (two captures disagreed) is retried, up to 3 attempts, because the tool documents 6 as safe to retry.

| config | exact on first try | exact within retries | exit 6 during the run | avg conf (successful reads) | avg ms |
| --- | --- | --- | --- | --- | --- |
| default | 12/20 | 12/20 | 3 | 90.6 | 593 |
| default+normalize | 12/20 | 12/20 | 0 | 91.7 | 598 |
| nearest-3x | 11/20 | 11/20 | 0 | 92.4 | 610 |
| bilinear-3x | 9/20 | 9/20 | 0 | 92.1 | 614 |
| bilinear-3x+normalize | 15/20 | 15/20 | 0 | 88.7 | 629 |

| # | sample | config | tries | last exit | read back | screenshots (every attempt) |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | 你好，世界 | default | 1 | 0 | 你好，世界 yes | `01-default-try1-raw` (400x32) `01-default-try1-prepared` (400x32) |
| 1 | 你好，世界 | default+normalize | 1 | 0 | 你好，世界 yes | `01-default+normalize-try1-raw` (400x32) `01-default+normalize-try1-prepared` (400x32) |
| 1 | 你好，世界 | nearest-3x | 3 | 0 | re,EF **no** | `01-nearest-3x-try1-raw` (400x32) `01-nearest-3x-try1-prepared` (1232x128)<br>`01-nearest-3x-try2-raw` (400x32) `01-nearest-3x-try2-prepared` (1232x128)<br>`01-nearest-3x-try3-raw` (400x32) `01-nearest-3x-try3-prepared` (1232x128) |
| 1 | 你好，世界 | bilinear-3x | 3 | 0 | We,HH **no** | `01-bilinear-3x-try1-raw` (400x32) `01-bilinear-3x-try1-prepared` (1232x128)<br>`01-bilinear-3x-try2-raw` (400x32) `01-bilinear-3x-try2-prepared` (1232x128)<br>`01-bilinear-3x-try3-raw` (400x32) `01-bilinear-3x-try3-prepared` (1232x128) |
| 1 | 你好，世界 | bilinear-3x+normalize | 1 | 0 | 你好，世界 yes | `01-bilinear-3x+normalize-try1-raw` (400x32) `01-bilinear-3x+normalize-try1-prepared` (1232x128) |
| 2 | ProbeCheck12345 | default | 1 | 0 | ProbeCheck12345 yes | `02-default-try1-raw` (400x32) `02-default-try1-prepared` (400x32) |
| 2 | ProbeCheck12345 | default+normalize | 1 | 0 | ProbeCheck12345 yes | `02-default+normalize-try1-raw` (400x32) `02-default+normalize-try1-prepared` (400x32) |
| 2 | ProbeCheck12345 | nearest-3x | 1 | 0 | ProbeCheck12345 yes | `02-nearest-3x-try1-raw` (400x32) `02-nearest-3x-try1-prepared` (1232x128) |
| 2 | ProbeCheck12345 | bilinear-3x | 1 | 0 | ProbeCheck12345 yes | `02-bilinear-3x-try1-raw` (400x32) `02-bilinear-3x-try1-prepared` (1232x128) |
| 2 | ProbeCheck12345 | bilinear-3x+normalize | 1 | 0 | ProbeCheck12345 yes | `02-bilinear-3x+normalize-try1-raw` (400x32) `02-bilinear-3x+normalize-try1-prepared` (1232x128) |
| 3 | 请输入文件名后点击保存按钮 | default | 3 | 0 | 请输入文件名后点击保存近钮 **no** | `03-default-try1-raw` (400x32) `03-default-try1-prepared` (400x32)<br>`03-default-try2-raw` (400x32) `03-default-try2-prepared` (400x32)<br>`03-default-try3-raw` (400x32) `03-default-try3-prepared` (400x32) |
| 3 | 请输入文件名后点击保存按钮 | default+normalize | 1 | 0 | 请输入文件名后点击保存按钮 yes | `03-default+normalize-try1-raw` (400x32) `03-default+normalize-try1-prepared` (400x32) |
| 3 | 请输入文件名后点击保存按钮 | nearest-3x | 1 | 0 | 请输入文件名后点击保存按钮 yes | `03-nearest-3x-try1-raw` (400x32) `03-nearest-3x-try1-prepared` (1232x128) |
| 3 | 请输入文件名后点击保存按钮 | bilinear-3x | 3 | 0 | 请输入文件名后点击保存控钮 **no** | `03-bilinear-3x-try1-raw` (400x32) `03-bilinear-3x-try1-prepared` (1232x128)<br>`03-bilinear-3x-try2-raw` (400x32) `03-bilinear-3x-try2-prepared` (1232x128)<br>`03-bilinear-3x-try3-raw` (400x32) `03-bilinear-3x-try3-prepared` (1232x128) |
| 3 | 请输入文件名后点击保存按钮 | bilinear-3x+normalize | 1 | 0 | 请输入文件名后点击保存按钮 yes | `03-bilinear-3x+normalize-try1-raw` (400x32) `03-bilinear-3x+normalize-try1-prepared` (1232x128) |
| 4 | 保存 取消 确定 | default | 1 | 0 | 保存取消确定 yes | `04-default-try1-raw` (400x32) `04-default-try1-prepared` (400x32) |
| 4 | 保存 取消 确定 | default+normalize | 1 | 0 | 保存取消确定 yes | `04-default+normalize-try1-raw` (400x32) `04-default+normalize-try1-prepared` (400x32) |
| 4 | 保存 取消 确定 | nearest-3x | 1 | 0 | 保存取消确定 yes | `04-nearest-3x-try1-raw` (400x32) `04-nearest-3x-try1-prepared` (1232x128) |
| 4 | 保存 取消 确定 | bilinear-3x | 1 | 0 | 保存取消确定 yes | `04-bilinear-3x-try1-raw` (400x32) `04-bilinear-3x-try1-prepared` (1232x128) |
| 4 | 保存 取消 确定 | bilinear-3x+normalize | 1 | 0 | 保存取消确定 yes | `04-bilinear-3x+normalize-try1-raw` (400x32) `04-bilinear-3x+normalize-try1-prepared` (1232x128) |
| 5 | Order #4471 shipped | default | 1 | 0 | Order#4471shipped yes | `05-default-try1-raw` (400x32) `05-default-try1-prepared` (400x32) |
| 5 | Order #4471 shipped | default+normalize | 1 | 0 | Order#4471shipped yes | `05-default+normalize-try1-raw` (400x32) `05-default+normalize-try1-prepared` (400x32) |
| 5 | Order #4471 shipped | nearest-3x | 1 | 0 | Order#4471shipped yes | `05-nearest-3x-try1-raw` (400x32) `05-nearest-3x-try1-prepared` (1232x128) |
| 5 | Order #4471 shipped | bilinear-3x | 1 | 0 | Order#4471shipped yes | `05-bilinear-3x-try1-raw` (400x32) `05-bilinear-3x-try1-prepared` (1232x128) |
| 5 | Order #4471 shipped | bilinear-3x+normalize | 1 | 0 | Order#4471shipped yes | `05-bilinear-3x+normalize-try1-raw` (400x32) `05-bilinear-3x+normalize-try1-prepared` (1232x128) |
| 6 | 区域太小会出现重影 | default | 1 | 0 | 区域太小会出现重影 yes | `06-default-try1-raw` (400x32) `06-default-try1-prepared` (400x32) |
| 6 | 区域太小会出现重影 | default+normalize | 1 | 0 | 区域太小会出现重影 yes | `06-default+normalize-try1-raw` (400x32) `06-default+normalize-try1-prepared` (400x32) |
| 6 | 区域太小会出现重影 | nearest-3x | 1 | 0 | 区域太小会出现重影 yes | `06-nearest-3x-try1-raw` (400x32) `06-nearest-3x-try1-prepared` (1232x128) |
| 6 | 区域太小会出现重影 | bilinear-3x | 1 | 0 | 区域太小会出现重影 yes | `06-bilinear-3x-try1-raw` (400x32) `06-bilinear-3x-try1-prepared` (1232x128) |
| 6 | 区域太小会出现重影 | bilinear-3x+normalize | 1 | 0 | 区域太小会出现重影 yes | `06-bilinear-3x+normalize-try1-raw` (400x32) `06-bilinear-3x+normalize-try1-prepared` (1232x128) |
| 7 | 文件 编辑 查看 H1 | default | 1 | 0 | 文件编辑查看H1 yes | `07-default-try1-raw` (400x32) `07-default-try1-prepared` (400x32) |
| 7 | 文件 编辑 查看 H1 | default+normalize | 3 | 0 | 文件编辑BBH1 **no** | `07-default+normalize-try1-raw` (400x32) `07-default+normalize-try1-prepared` (400x32)<br>`07-default+normalize-try2-raw` (400x32) `07-default+normalize-try2-prepared` (400x32)<br>`07-default+normalize-try3-raw` (400x32) `07-default+normalize-try3-prepared` (400x32) |
| 7 | 文件 编辑 查看 H1 | nearest-3x | 1 | 0 | 文件编辑查看H1 yes | `07-nearest-3x-try1-raw` (400x32) `07-nearest-3x-try1-prepared` (1232x128) |
| 7 | 文件 编辑 查看 H1 | bilinear-3x | 1 | 0 | 文件编辑查看H1 yes | `07-bilinear-3x-try1-raw` (400x32) `07-bilinear-3x-try1-prepared` (1232x128) |
| 7 | 文件 编辑 查看 H1 | bilinear-3x+normalize | 1 | 0 | 文件编辑查看H1 yes | `07-bilinear-3x+normalize-try1-raw` (400x32) `07-bilinear-3x+normalize-try1-prepared` (1232x128) |
| 8 | 新建 文本文档.txt | default | 1 | 0 | 新建文本文档.txt yes | `08-default-try1-raw` (400x32) `08-default-try1-prepared` (400x32) |
| 8 | 新建 文本文档.txt | default+normalize | 1 | 0 | 新建文本文档.txt yes | `08-default+normalize-try1-raw` (400x32) `08-default+normalize-try1-prepared` (400x32) |
| 8 | 新建 文本文档.txt | nearest-3x | 1 | 0 | 新建文本文档.txt yes | `08-nearest-3x-try1-raw` (400x32) `08-nearest-3x-try1-prepared` (1232x128) |
| 8 | 新建 文本文档.txt | bilinear-3x | 1 | 0 | 新建文本文档.txt yes | `08-bilinear-3x-try1-raw` (400x32) `08-bilinear-3x-try1-prepared` (1232x128) |
| 8 | 新建 文本文档.txt | bilinear-3x+normalize | 1 | 0 | 新建文本文档.txt yes | `08-bilinear-3x+normalize-try1-raw` (400x32) `08-bilinear-3x+normalize-try1-prepared` (1232x128) |
| 9 | 247 * 119 | default | 1 | 0 | 247*119 yes | `09-default-try1-raw` (400x32) `09-default-try1-prepared` (400x32) |
| 9 | 247 * 119 | default+normalize | 1 | 0 | 247*119 yes | `09-default+normalize-try1-raw` (400x32) `09-default+normalize-try1-prepared` (400x32) |
| 9 | 247 * 119 | nearest-3x | 1 | 0 | 247*119 yes | `09-nearest-3x-try1-raw` (400x32) `09-nearest-3x-try1-prepared` (1232x128) |
| 9 | 247 * 119 | bilinear-3x | 1 | 0 | 247*119 yes | `09-bilinear-3x-try1-raw` (400x32) `09-bilinear-3x-try1-prepared` (1232x128) |
| 9 | 247 * 119 | bilinear-3x+normalize | 1 | 0 | 247*119 yes | `09-bilinear-3x+normalize-try1-raw` (400x32) `09-bilinear-3x+normalize-try1-prepared` (1232x128) |
| 10 | 圆角 阴影 重影 | default | 3 | 0 | aA阴影重影 **no** | `10-default-try1-raw` (400x32) `10-default-try1-prepared` (400x32)<br>`10-default-try2-raw` (400x32) `10-default-try2-prepared` (400x32)<br>`10-default-try3-raw` (400x32) `10-default-try3-prepared` (400x32) |
| 10 | 圆角 阴影 重影 | default+normalize | 1 | 0 | 圆角阴影重影 yes | `10-default+normalize-try1-raw` (400x32) `10-default+normalize-try1-prepared` (400x32) |
| 10 | 圆角 阴影 重影 | nearest-3x | 3 | 0 | AA阴影Be **no** | `10-nearest-3x-try1-raw` (400x32) `10-nearest-3x-try1-prepared` (1232x128)<br>`10-nearest-3x-try2-raw` (400x32) `10-nearest-3x-try2-prepared` (1232x128)<br>`10-nearest-3x-try3-raw` (400x32) `10-nearest-3x-try3-prepared` (1232x128) |
| 10 | 圆角 阴影 重影 | bilinear-3x | 3 | 0 | AA阴影重影 **no** | `10-bilinear-3x-try1-raw` (400x32) `10-bilinear-3x-try1-prepared` (1232x128)<br>`10-bilinear-3x-try2-raw` (400x32) `10-bilinear-3x-try2-prepared` (1232x128)<br>`10-bilinear-3x-try3-raw` (400x32) `10-bilinear-3x-try3-prepared` (1232x128) |
| 10 | 圆角 阴影 重影 | bilinear-3x+normalize | 1 | 0 | 圆角阴影重影 yes | `10-bilinear-3x+normalize-try1-raw` (400x32) `10-bilinear-3x+normalize-try1-prepared` (1232x128) |
| 11 | error: 找不到文件 (0x2) | default | 1 | 0 | error:找不到文件(0x2) yes | `11-default-try1-raw` (400x32) `11-default-try1-prepared` (400x32) |
| 11 | error: 找不到文件 (0x2) | default+normalize | 3 | 0 | error:找不到文件(ex2) **no** | `11-default+normalize-try1-raw` (400x32) `11-default+normalize-try1-prepared` (400x32)<br>`11-default+normalize-try2-raw` (400x32) `11-default+normalize-try2-prepared` (400x32)<br>`11-default+normalize-try3-raw` (400x32) `11-default+normalize-try3-prepared` (400x32) |
| 11 | error: 找不到文件 (0x2) | nearest-3x | 1 | 0 | error:找不到文件(0x2) yes | `11-nearest-3x-try1-raw` (400x32) `11-nearest-3x-try1-prepared` (1232x128) |
| 11 | error: 找不到文件 (0x2) | bilinear-3x | 3 | 0 | error:找不到文件(6x2) **no** | `11-bilinear-3x-try1-raw` (400x32) `11-bilinear-3x-try1-prepared` (1232x128)<br>`11-bilinear-3x-try2-raw` (400x32) `11-bilinear-3x-try2-prepared` (1232x128)<br>`11-bilinear-3x-try3-raw` (400x32) `11-bilinear-3x-try3-prepared` (1232x128) |
| 11 | error: 找不到文件 (0x2) | bilinear-3x+normalize | 1 | 0 | error:找不到文件(0x2) yes | `11-bilinear-3x+normalize-try1-raw` (400x32) `11-bilinear-3x+normalize-try1-prepared` (1232x128) |
| 12 | 用户名或密码不正确 | default | 1 | 0 | 用户名或密码不正确 yes | `12-default-try1-raw` (400x32) `12-default-try1-prepared` (400x32) |
| 12 | 用户名或密码不正确 | default+normalize | 1 | 0 | 用户名或密码不正确 yes | `12-default+normalize-try1-raw` (400x32) `12-default+normalize-try1-prepared` (400x32) |
| 12 | 用户名或密码不正确 | nearest-3x | 1 | 0 | 用户名或密码不正确 yes | `12-nearest-3x-try1-raw` (400x32) `12-nearest-3x-try1-prepared` (1232x128) |
| 12 | 用户名或密码不正确 | bilinear-3x | 1 | 0 | 用户名或密码不正确 yes | `12-bilinear-3x-try1-raw` (400x32) `12-bilinear-3x-try1-prepared` (1232x128) |
| 12 | 用户名或密码不正确 | bilinear-3x+normalize | 1 | 0 | 用户名或密码不正确 yes | `12-bilinear-3x+normalize-try1-raw` (400x32) `12-bilinear-3x+normalize-try1-prepared` (1232x128) |
| 13 | KeyMouse v2.0.0 已就绪 | default | 1 | 0 | KeyMousev2.0.0已就绪 yes | `13-default-try1-raw` (400x32) `13-default-try1-prepared` (400x32) |
| 13 | KeyMouse v2.0.0 已就绪 | default+normalize | 1 | 0 | KeyMousev2.0.0已就绪 yes | `13-default+normalize-try1-raw` (400x32) `13-default+normalize-try1-prepared` (400x32) |
| 13 | KeyMouse v2.0.0 已就绪 | nearest-3x | 3 | 0 | KeyMousev2.0.0ORS **no** | `13-nearest-3x-try1-raw` (400x32) `13-nearest-3x-try1-prepared` (1232x128)<br>`13-nearest-3x-try2-raw` (400x32) `13-nearest-3x-try2-prepared` (1232x128)<br>`13-nearest-3x-try3-raw` (400x32) `13-nearest-3x-try3-prepared` (1232x128) |
| 13 | KeyMouse v2.0.0 已就绪 | bilinear-3x | 1 | 0 | KeyMousev2.0.0已就绪 yes | `13-bilinear-3x-try1-raw` (400x32) `13-bilinear-3x-try1-prepared` (1232x128) |
| 13 | KeyMouse v2.0.0 已就绪 | bilinear-3x+normalize | 1 | 0 | KeyMousev2.0.0已就绪 yes | `13-bilinear-3x+normalize-try1-raw` (400x32) `13-bilinear-3x+normalize-try1-prepared` (1232x128) |
| 14 | 导出为 PNG/JPG，质量 85% | default | 1 | 0 | 导出为PNG/JPG，质量85% yes | `14-default-try1-raw` (400x32) `14-default-try1-prepared` (400x32) |
| 14 | 导出为 PNG/JPG，质量 85% | default+normalize | 3 | 0 | SHAPNG/IPG,FREE85% **no** | `14-default+normalize-try1-raw` (400x32) `14-default+normalize-try1-prepared` (400x32)<br>`14-default+normalize-try2-raw` (400x32) `14-default+normalize-try2-prepared` (400x32)<br>`14-default+normalize-try3-raw` (400x32) `14-default+normalize-try3-prepared` (400x32) |
| 14 | 导出为 PNG/JPG，质量 85% | nearest-3x | 3 | 0 | 导出为PNG/IPG,ME85% **no** | `14-nearest-3x-try1-raw` (400x32) `14-nearest-3x-try1-prepared` (1232x128)<br>`14-nearest-3x-try2-raw` (400x32) `14-nearest-3x-try2-prepared` (1232x128)<br>`14-nearest-3x-try3-raw` (400x32) `14-nearest-3x-try3-prepared` (1232x128) |
| 14 | 导出为 PNG/JPG，质量 85% | bilinear-3x | 3 | 0 | BHAPNG/IPG,ME85% **no** | `14-bilinear-3x-try1-raw` (400x32) `14-bilinear-3x-try1-prepared` (1232x128)<br>`14-bilinear-3x-try2-raw` (400x32) `14-bilinear-3x-try2-prepared` (1232x128)<br>`14-bilinear-3x-try3-raw` (400x32) `14-bilinear-3x-try3-prepared` (1232x128) |
| 14 | 导出为 PNG/JPG，质量 85% | bilinear-3x+normalize | 3 | 0 | 导出为PNG/]PG，质量85% **no** | `14-bilinear-3x+normalize-try1-raw` (400x32) `14-bilinear-3x+normalize-try1-prepared` (1232x128)<br>`14-bilinear-3x+normalize-try2-raw` (400x32) `14-bilinear-3x+normalize-try2-prepared` (1232x128)<br>`14-bilinear-3x+normalize-try3-raw` (400x32) `14-bilinear-3x+normalize-try3-prepared` (1232x128) |
| 15 | 第 3 步：确认后点击"下一步" | default | 3 | 0 | 第3步:确认后点击"下一步" **no** | `15-default-try1-raw` (400x32) `15-default-try1-prepared` (400x32)<br>`15-default-try2-raw` (400x32) `15-default-try2-prepared` (400x32)<br>`15-default-try3-raw` (400x32) `15-default-try3-prepared` (400x32) |
| 15 | 第 3 步：确认后点击"下一步" | default+normalize | 3 | 0 | 632:MUAH"Fs" **no** | `15-default+normalize-try1-raw` (400x32) `15-default+normalize-try1-prepared` (400x32)<br>`15-default+normalize-try2-raw` (400x32) `15-default+normalize-try2-prepared` (400x32)<br>`15-default+normalize-try3-raw` (400x32) `15-default+normalize-try3-prepared` (400x32) |
| 15 | 第 3 步：确认后点击"下一步" | nearest-3x | 3 | 0 | 第3步:确认后点击"下一步" **no** | `15-nearest-3x-try1-raw` (400x32) `15-nearest-3x-try1-prepared` (1232x128)<br>`15-nearest-3x-try2-raw` (400x32) `15-nearest-3x-try2-prepared` (1232x128)<br>`15-nearest-3x-try3-raw` (400x32) `15-nearest-3x-try3-prepared` (1232x128) |
| 15 | 第 3 步：确认后点击"下一步" | bilinear-3x | 3 | 0 | 第3步:确认后点击"下一步" **no** | `15-bilinear-3x-try1-raw` (400x32) `15-bilinear-3x-try1-prepared` (1232x128)<br>`15-bilinear-3x-try2-raw` (400x32) `15-bilinear-3x-try2-prepared` (1232x128)<br>`15-bilinear-3x-try3-raw` (400x32) `15-bilinear-3x-try3-prepared` (1232x128) |
| 15 | 第 3 步：确认后点击"下一步" | bilinear-3x+normalize | 3 | 0 | 第3步:确认后点击"下一步" **no** | `15-bilinear-3x+normalize-try1-raw` (400x32) `15-bilinear-3x+normalize-try1-prepared` (1232x128)<br>`15-bilinear-3x+normalize-try2-raw` (400x32) `15-bilinear-3x+normalize-try2-prepared` (1232x128)<br>`15-bilinear-3x+normalize-try3-raw` (400x32) `15-bilinear-3x+normalize-try3-prepared` (1232x128) |
| 16 | C:\Users\xumiao\Desktop\报告.docx | default | 3 | 6 |  **no** | `16-default-try1-raw` (400x32) `16-default-try1-prepared` (400x32)<br>`16-default-try2-raw` (400x32) `16-default-try2-prepared` (400x32)<br>`16-default-try3-raw` (400x32) `16-default-try3-prepared` (400x32) |
| 16 | C:\Users\xumiao\Desktop\报告.docx | default+normalize | 3 | 0 | C:\Users\xumiao\Desktop\sE=.- **no** | `16-default+normalize-try1-raw` (400x32) `16-default+normalize-try1-prepared` (400x32)<br>`16-default+normalize-try2-raw` (400x32) `16-default+normalize-try2-prepared` (400x32)<br>`16-default+normalize-try3-raw` (400x32) `16-default+normalize-try3-prepared` (400x32) |
| 16 | C:\Users\xumiao\Desktop\报告.docx | nearest-3x | 3 | 0 | C:\Users\xumiao\Desktop\#£&. **no** | `16-nearest-3x-try1-raw` (400x32) `16-nearest-3x-try1-prepared` (1232x128)<br>`16-nearest-3x-try2-raw` (400x32) `16-nearest-3x-try2-prepared` (1232x128)<br>`16-nearest-3x-try3-raw` (400x32) `16-nearest-3x-try3-prepared` (1232x128) |
| 16 | C:\Users\xumiao\Desktop\报告.docx | bilinear-3x | 3 | 0 | C:\Users\xumiao\Desktop\i£S. **no** | `16-bilinear-3x-try1-raw` (400x32) `16-bilinear-3x-try1-prepared` (1232x128)<br>`16-bilinear-3x-try2-raw` (400x32) `16-bilinear-3x-try2-prepared` (1232x128)<br>`16-bilinear-3x-try3-raw` (400x32) `16-bilinear-3x-try3-prepared` (1232x128) |
| 16 | C:\Users\xumiao\Desktop\报告.docx | bilinear-3x+normalize | 3 | 0 | C:\Users\xumiao\Desktop\ik&.- **no** | `16-bilinear-3x+normalize-try1-raw` (400x32) `16-bilinear-3x+normalize-try1-prepared` (1232x128)<br>`16-bilinear-3x+normalize-try2-raw` (400x32) `16-bilinear-3x+normalize-try2-prepared` (1232x128)<br>`16-bilinear-3x+normalize-try3-raw` (400x32) `16-bilinear-3x+normalize-try3-prepared` (1232x128) |
| 17 | 总计 1,234.56 元 | default | 3 | 0 | Bit1,234.56元 **no** | `17-default-try1-raw` (400x32) `17-default-try1-prepared` (400x32)<br>`17-default-try2-raw` (400x32) `17-default-try2-prepared` (400x32)<br>`17-default-try3-raw` (400x32) `17-default-try3-prepared` (400x32) |
| 17 | 总计 1,234.56 元 | default+normalize | 1 | 0 | 总计1,234.56元 yes | `17-default+normalize-try1-raw` (400x32) `17-default+normalize-try1-prepared` (400x32) |
| 17 | 总计 1,234.56 元 | nearest-3x | 1 | 0 | 总计1,234.56元 yes | `17-nearest-3x-try1-raw` (400x32) `17-nearest-3x-try1-prepared` (1232x128) |
| 17 | 总计 1,234.56 元 | bilinear-3x | 3 | 0 | Sit1,234.56元 **no** | `17-bilinear-3x-try1-raw` (400x32) `17-bilinear-3x-try1-prepared` (1232x128)<br>`17-bilinear-3x-try2-raw` (400x32) `17-bilinear-3x-try2-prepared` (1232x128)<br>`17-bilinear-3x-try3-raw` (400x32) `17-bilinear-3x-try3-prepared` (1232x128) |
| 17 | 总计 1,234.56 元 | bilinear-3x+normalize | 1 | 0 | 总计1,234.56元 yes | `17-bilinear-3x+normalize-try1-raw` (400x32) `17-bilinear-3x+normalize-try1-prepared` (1232x128) |
| 18 | 是否保存更改？[是] [否] [取消] | default | 3 | 0 | 是否保存更改?[是](3)[取消 **no** | `18-default-try1-raw` (400x32) `18-default-try1-prepared` (400x32)<br>`18-default-try2-raw` (400x32) `18-default-try2-prepared` (400x32)<br>`18-default-try3-raw` (400x32) `18-default-try3-prepared` (400x32) |
| 18 | 是否保存更改？[是] [否] [取消] | default+normalize | 3 | 0 | 是否保存更改?(2)(8B)[取消 **no** | `18-default+normalize-try1-raw` (400x32) `18-default+normalize-try1-prepared` (400x32)<br>`18-default+normalize-try2-raw` (400x32) `18-default+normalize-try2-prepared` (400x32)<br>`18-default+normalize-try3-raw` (400x32) `18-default+normalize-try3-prepared` (400x32) |
| 18 | 是否保存更改？[是] [否] [取消] | nearest-3x | 3 | 0 | 是否保存更改?[是][否][取消 **no** | `18-nearest-3x-try1-raw` (400x32) `18-nearest-3x-try1-prepared` (1232x128)<br>`18-nearest-3x-try2-raw` (400x32) `18-nearest-3x-try2-prepared` (1232x128)<br>`18-nearest-3x-try3-raw` (400x32) `18-nearest-3x-try3-prepared` (1232x128) |
| 18 | 是否保存更改？[是] [否] [取消] | bilinear-3x | 3 | 0 | 是否保存更改?[是][否][取消 **no** | `18-bilinear-3x-try1-raw` (400x32) `18-bilinear-3x-try1-prepared` (1232x128)<br>`18-bilinear-3x-try2-raw` (400x32) `18-bilinear-3x-try2-prepared` (1232x128)<br>`18-bilinear-3x-try3-raw` (400x32) `18-bilinear-3x-try3-prepared` (1232x128) |
| 18 | 是否保存更改？[是] [否] [取消] | bilinear-3x+normalize | 3 | 0 | 是否保存更改?[是][否][取消 **no** | `18-bilinear-3x+normalize-try1-raw` (400x32) `18-bilinear-3x+normalize-try1-prepared` (1232x128)<br>`18-bilinear-3x+normalize-try2-raw` (400x32) `18-bilinear-3x+normalize-try2-prepared` (1232x128)<br>`18-bilinear-3x+normalize-try3-raw` (400x32) `18-bilinear-3x+normalize-try3-prepared` (1232x128) |
| 19 | 连接超时，请重试（第 2/5 次） | default | 3 | 0 | 连接超时，请重试〈第2/5次) **no** | `19-default-try1-raw` (400x32) `19-default-try1-prepared` (400x32)<br>`19-default-try2-raw` (400x32) `19-default-try2-prepared` (400x32)<br>`19-default-try3-raw` (400x32) `19-default-try3-prepared` (400x32) |
| 19 | 连接超时，请重试（第 2/5 次） | default+normalize | 3 | 0 | 连接超时，请重试〈第2/5次) **no** | `19-default+normalize-try1-raw` (400x32) `19-default+normalize-try1-prepared` (400x32)<br>`19-default+normalize-try2-raw` (400x32) `19-default+normalize-try2-prepared` (400x32)<br>`19-default+normalize-try3-raw` (400x32) `19-default+normalize-try3-prepared` (400x32) |
| 19 | 连接超时，请重试（第 2/5 次） | nearest-3x | 3 | 0 | 连接超时，请重试〈第2/5次) **no** | `19-nearest-3x-try1-raw` (400x32) `19-nearest-3x-try1-prepared` (1232x128)<br>`19-nearest-3x-try2-raw` (400x32) `19-nearest-3x-try2-prepared` (1232x128)<br>`19-nearest-3x-try3-raw` (400x32) `19-nearest-3x-try3-prepared` (1232x128) |
| 19 | 连接超时，请重试（第 2/5 次） | bilinear-3x | 3 | 0 | 连接超时，请重试(SF2/5次) **no** | `19-bilinear-3x-try1-raw` (400x32) `19-bilinear-3x-try1-prepared` (1232x128)<br>`19-bilinear-3x-try2-raw` (400x32) `19-bilinear-3x-try2-prepared` (1232x128)<br>`19-bilinear-3x-try3-raw` (400x32) `19-bilinear-3x-try3-prepared` (1232x128) |
| 19 | 连接超时，请重试（第 2/5 次） | bilinear-3x+normalize | 3 | 0 | 连接超时，请重试〈第2/5次) **no** | `19-bilinear-3x+normalize-try1-raw` (400x32) `19-bilinear-3x+normalize-try1-prepared` (1232x128)<br>`19-bilinear-3x+normalize-try2-raw` (400x32) `19-bilinear-3x+normalize-try2-prepared` (1232x128)<br>`19-bilinear-3x+normalize-try3-raw` (400x32) `19-bilinear-3x+normalize-try3-prepared` (1232x128) |
| 20 | Ctrl+S 保存，Ctrl+Z 撤销 | default | 3 | 0 | Ctrl+sS4#%,Ctrl+Z撤销 **no** | `20-default-try1-raw` (400x32) `20-default-try1-prepared` (400x32)<br>`20-default-try2-raw` (400x32) `20-default-try2-prepared` (400x32)<br>`20-default-try3-raw` (400x32) `20-default-try3-prepared` (400x32) |
| 20 | Ctrl+S 保存，Ctrl+Z 撤销 | default+normalize | 3 | 0 | Ctrl+sS保存，Ctrl+Z撤销 **no** | `20-default+normalize-try1-raw` (400x32) `20-default+normalize-try1-prepared` (400x32)<br>`20-default+normalize-try2-raw` (400x32) `20-default+normalize-try2-prepared` (400x32)<br>`20-default+normalize-try3-raw` (400x32) `20-default+normalize-try3-prepared` (400x32) |
| 20 | Ctrl+S 保存，Ctrl+Z 撤销 | nearest-3x | 3 | 0 | Ctrl+SGA,Ctrl+Z撤销 **no** | `20-nearest-3x-try1-raw` (400x32) `20-nearest-3x-try1-prepared` (1232x128)<br>`20-nearest-3x-try2-raw` (400x32) `20-nearest-3x-try2-prepared` (1232x128)<br>`20-nearest-3x-try3-raw` (400x32) `20-nearest-3x-try3-prepared` (1232x128) |
| 20 | Ctrl+S 保存，Ctrl+Z 撤销 | bilinear-3x | 3 | 0 | Ctrl+S保存，Ctrl+ZAS **no** | `20-bilinear-3x-try1-raw` (400x32) `20-bilinear-3x-try1-prepared` (1232x128)<br>`20-bilinear-3x-try2-raw` (400x32) `20-bilinear-3x-try2-prepared` (1232x128)<br>`20-bilinear-3x-try3-raw` (400x32) `20-bilinear-3x-try3-prepared` (1232x128) |
| 20 | Ctrl+S 保存，Ctrl+Z 撤销 | bilinear-3x+normalize | 1 | 0 | Ctrl+S保存，Ctrl+Z撤销 yes | `20-bilinear-3x+normalize-try1-raw` (400x32) `20-bilinear-3x+normalize-try1-prepared` (1232x128) |

## Caret

The same region, read with the text caret inside it. The caret blinks, so consecutive captures
disagree (exit 6), and - measured - a frame with the caret can make the engine misread the whole line.

| frame | caret | read back | conf | screenshot |
| --- | --- | --- | --- | --- |
| frame 1 | see screenshot | 你好，世界 | 90.3 | ``images/caret-frame1.png`` |
| frame 2 | see screenshot | Re,th | 48 | ``images/caret-frame2.png`` |
| after three newlines (caret below the band) | no | 你好，世界 | 90.3 | |

A band that is too short clips the glyphs and reads as garbage too: 400x20 over the same line gave
`4eim+s` where 400x32 reads it correctly, so the height has to cover the full line box.

## Who decides Chinese vs English

The same mixed line (`你好abc世界123 ABC`) read with three language sets. KeyMouse passes `--lang`
straight to the engine, never classifies a character itself, and echoes the set in the JSON.

| --lang | engine languages | exit | read back | conf | screenshot |
| --- | --- | --- | --- | --- | --- |
| eng | eng | 0 | URtxabcth2123ABC | 43.8 | ``images/language-eng-raw.png`` |
| chi_sim | chi_sim | 0 | 你好abc世界123ABC | 80.4 | ``images/language-chi_sim-raw.png`` |
| eng+chi_sim | eng+chi_sim | 0 | URtxabcth2123ABC | 43.8 | ``images/language-eng-chi_sim-raw.png`` |

## Dark background (light text on dark), read raw versus normalised

The picker overlay draws white text on a dark band, so it is a dark-background sample whose
wording is known. `--normalize` is off by default now; this is what it buys.

| mode | exit | read back | conf | screenshot |
| --- | --- | --- | --- | --- |
| raw | 0 | 拖动框选区域“。单击=BeOS-ESC取消= | 72 | ``images/dark-raw-raw.png`` |
| normalized | 0 | 拖动框选区域“。单击=选中该窗口的整个客户区-Esc取消[和 | 79.3 | ``images/dark-normalized-raw.png`` |

## Region picker

- `region pick --rect` in client space: exit 0, space client, region 60,60,200,30
- `region pick --rect` in window space (title bar): exit 0, space window, region 40,5,200,20
- `region pick --rect 0,0,0,5`: exit 2 (a zero-sized rectangle is a usage error)
- dragged rectangle: exit 0, space client, region 60,200,200,30
- the overlay itself: `images/region-picker-overlay.png` (probe read exit 0)
- ESC cancels: exit 3

## Coordinate spaces

- client space image:  `8FFC6D0CEE4847EC91BEBFC6310054F6B05AE95BC6203F7FFED45EB7B68CF415`  (`images/space-client-raw.png`)
- window space image:  `8FFC6D0CEE4847EC91BEBFC6310054F6B05AE95BC6203F7FFED45EB7B68CF415`  (`images/space-window-raw.png`)
- identical: yes

## Title bar read

- exit 0, confidence 70.1, text `a® KeyMouse 冒 烟 靶 子` (`images/title-bar-raw.png`)

## Files

- `commands.txt` - every command line with exit code, stdout and stderr
- `json/` - the JSON of every call, and the `.err.txt` of every failure
- `images/` - every screenshot, as PNG; the `-raw` files are the pixels as captured
