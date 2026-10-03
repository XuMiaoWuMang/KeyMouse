# KeyMouse evidence 20261003-165632

Command: `pwsh tests/evidence.ps1 -Samples 10 -Attempts 3`
Machine: DESKTOP-MF86BEL, PowerShell 7.6.6

## Upscale sampling

Text is typed into the smoke target, read back from region 0,0,400,32 and compared character for character.
A read that exits 6 (two captures disagreed) is retried, up to 3 attempts, because the tool documents 6 as safe to retry.

| config | exact on first try | exact within retries | exit 6 during the run | avg conf (successful reads) | avg ms |
| --- | --- | --- | --- | --- | --- |
| raw-1x | 9/10 | 9/10 | 0 | 91.2 | 580 |
| nearest-2x | 9/10 | 9/10 | 0 | 93.5 | 586 |
| nearest-3x | 9/10 | 9/10 | 0 | 91.0 | 598 |
| nearest-4x | 8/10 | 8/10 | 0 | 89.9 | 603 |
| bilinear-3x | 10/10 | 10/10 | 0 | 88.4 | 612 |

| # | sample | config | tries | last exit | read back | screenshots (every attempt) |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | 你好，世界 | raw-1x | 1 | 0 | 你好，世界 yes | `01-raw-1x-try1-raw` (400x32) `01-raw-1x-try1-prepared` (400x32) |
| 1 | 你好，世界 | nearest-2x | 1 | 0 | 你好，世界 yes | `01-nearest-2x-try1-raw` (400x32) `01-nearest-2x-try1-prepared` (832x96) |
| 1 | 你好，世界 | nearest-3x | 1 | 0 | 你好，世界 yes | `01-nearest-3x-try1-raw` (400x32) `01-nearest-3x-try1-prepared` (1232x128) |
| 1 | 你好，世界 | nearest-4x | 1 | 0 | 你好，世界 yes | `01-nearest-4x-try1-raw` (400x32) `01-nearest-4x-try1-prepared` (1632x160) |
| 1 | 你好，世界 | bilinear-3x | 1 | 0 | 你好，世界 yes | `01-bilinear-3x-try1-raw` (400x32) `01-bilinear-3x-try1-prepared` (1232x128) |
| 2 | ProbeCheck12345 | raw-1x | 1 | 0 | ProbeCheck12345 yes | `02-raw-1x-try1-raw` (400x32) `02-raw-1x-try1-prepared` (400x32) |
| 2 | ProbeCheck12345 | nearest-2x | 1 | 0 | ProbeCheck12345 yes | `02-nearest-2x-try1-raw` (400x32) `02-nearest-2x-try1-prepared` (832x96) |
| 2 | ProbeCheck12345 | nearest-3x | 1 | 0 | ProbeCheck12345 yes | `02-nearest-3x-try1-raw` (400x32) `02-nearest-3x-try1-prepared` (1232x128) |
| 2 | ProbeCheck12345 | nearest-4x | 1 | 0 | ProbeCheck12345 yes | `02-nearest-4x-try1-raw` (400x32) `02-nearest-4x-try1-prepared` (1632x160) |
| 2 | ProbeCheck12345 | bilinear-3x | 1 | 0 | ProbeCheck12345 yes | `02-bilinear-3x-try1-raw` (400x32) `02-bilinear-3x-try1-prepared` (1232x128) |
| 3 | 请输入文件名后点击保存按钮 | raw-1x | 1 | 0 | 请输入文件名后点击保存按钮 yes | `03-raw-1x-try1-raw` (400x32) `03-raw-1x-try1-prepared` (400x32) |
| 3 | 请输入文件名后点击保存按钮 | nearest-2x | 1 | 0 | 请输入文件名后点击保存按钮 yes | `03-nearest-2x-try1-raw` (400x32) `03-nearest-2x-try1-prepared` (832x96) |
| 3 | 请输入文件名后点击保存按钮 | nearest-3x | 1 | 0 | 请输入文件名后点击保存按钮 yes | `03-nearest-3x-try1-raw` (400x32) `03-nearest-3x-try1-prepared` (1232x128) |
| 3 | 请输入文件名后点击保存按钮 | nearest-4x | 1 | 0 | 请输入文件名后点击保存按钮 yes | `03-nearest-4x-try1-raw` (400x32) `03-nearest-4x-try1-prepared` (1632x160) |
| 3 | 请输入文件名后点击保存按钮 | bilinear-3x | 1 | 0 | 请输入文件名后点击保存按钮 yes | `03-bilinear-3x-try1-raw` (400x32) `03-bilinear-3x-try1-prepared` (1232x128) |
| 4 | 保存 取消 确定 | raw-1x | 1 | 0 | 保存取消确定 yes | `04-raw-1x-try1-raw` (400x32) `04-raw-1x-try1-prepared` (400x32) |
| 4 | 保存 取消 确定 | nearest-2x | 1 | 0 | 保存取消确定 yes | `04-nearest-2x-try1-raw` (400x32) `04-nearest-2x-try1-prepared` (832x96) |
| 4 | 保存 取消 确定 | nearest-3x | 1 | 0 | 保存取消确定 yes | `04-nearest-3x-try1-raw` (400x32) `04-nearest-3x-try1-prepared` (1232x128) |
| 4 | 保存 取消 确定 | nearest-4x | 1 | 0 | 保存取消确定 yes | `04-nearest-4x-try1-raw` (400x32) `04-nearest-4x-try1-prepared` (1632x160) |
| 4 | 保存 取消 确定 | bilinear-3x | 1 | 0 | 保存取消确定 yes | `04-bilinear-3x-try1-raw` (400x32) `04-bilinear-3x-try1-prepared` (1232x128) |
| 5 | Order #4471 shipped | raw-1x | 1 | 0 | Order#4471shipped yes | `05-raw-1x-try1-raw` (400x32) `05-raw-1x-try1-prepared` (400x32) |
| 5 | Order #4471 shipped | nearest-2x | 1 | 0 | Order#4471shipped yes | `05-nearest-2x-try1-raw` (400x32) `05-nearest-2x-try1-prepared` (832x96) |
| 5 | Order #4471 shipped | nearest-3x | 1 | 0 | Order#4471shipped yes | `05-nearest-3x-try1-raw` (400x32) `05-nearest-3x-try1-prepared` (1232x128) |
| 5 | Order #4471 shipped | nearest-4x | 1 | 0 | Order#4471shipped yes | `05-nearest-4x-try1-raw` (400x32) `05-nearest-4x-try1-prepared` (1632x160) |
| 5 | Order #4471 shipped | bilinear-3x | 1 | 0 | Order#4471shipped yes | `05-bilinear-3x-try1-raw` (400x32) `05-bilinear-3x-try1-prepared` (1232x128) |
| 6 | 区域太小会出现重影 | raw-1x | 1 | 0 | 区域太小会出现重影 yes | `06-raw-1x-try1-raw` (400x32) `06-raw-1x-try1-prepared` (400x32) |
| 6 | 区域太小会出现重影 | nearest-2x | 1 | 0 | 区域太小会出现重影 yes | `06-nearest-2x-try1-raw` (400x32) `06-nearest-2x-try1-prepared` (832x96) |
| 6 | 区域太小会出现重影 | nearest-3x | 1 | 0 | 区域太小会出现重影 yes | `06-nearest-3x-try1-raw` (400x32) `06-nearest-3x-try1-prepared` (1232x128) |
| 6 | 区域太小会出现重影 | nearest-4x | 1 | 0 | 区域太小会出现重影 yes | `06-nearest-4x-try1-raw` (400x32) `06-nearest-4x-try1-prepared` (1632x160) |
| 6 | 区域太小会出现重影 | bilinear-3x | 1 | 0 | 区域太小会出现重影 yes | `06-bilinear-3x-try1-raw` (400x32) `06-bilinear-3x-try1-prepared` (1232x128) |
| 7 | 文件 编辑 查看 H1 | raw-1x | 3 | 0 | 文件编辑BBH1 **no** | `07-raw-1x-try1-raw` (400x32) `07-raw-1x-try1-prepared` (400x32)<br>`07-raw-1x-try2-raw` (400x32) `07-raw-1x-try2-prepared` (400x32)<br>`07-raw-1x-try3-raw` (400x32) `07-raw-1x-try3-prepared` (400x32) |
| 7 | 文件 编辑 查看 H1 | nearest-2x | 1 | 0 | 文件编辑查看H1 yes | `07-nearest-2x-try1-raw` (400x32) `07-nearest-2x-try1-prepared` (832x96) |
| 7 | 文件 编辑 查看 H1 | nearest-3x | 3 | 0 | 文件编辑BAH1 **no** | `07-nearest-3x-try1-raw` (400x32) `07-nearest-3x-try1-prepared` (1232x128)<br>`07-nearest-3x-try2-raw` (400x32) `07-nearest-3x-try2-prepared` (1232x128)<br>`07-nearest-3x-try3-raw` (400x32) `07-nearest-3x-try3-prepared` (1232x128) |
| 7 | 文件 编辑 查看 H1 | nearest-4x | 3 | 0 | 文件编辑BAH1 **no** | `07-nearest-4x-try1-raw` (400x32) `07-nearest-4x-try1-prepared` (1632x160)<br>`07-nearest-4x-try2-raw` (400x32) `07-nearest-4x-try2-prepared` (1632x160)<br>`07-nearest-4x-try3-raw` (400x32) `07-nearest-4x-try3-prepared` (1632x160) |
| 7 | 文件 编辑 查看 H1 | bilinear-3x | 1 | 0 | 文件编辑查看H1 yes | `07-bilinear-3x-try1-raw` (400x32) `07-bilinear-3x-try1-prepared` (1232x128) |
| 8 | 新建 文本文档.txt | raw-1x | 1 | 0 | 新建文本文档.txt yes | `08-raw-1x-try1-raw` (400x32) `08-raw-1x-try1-prepared` (400x32) |
| 8 | 新建 文本文档.txt | nearest-2x | 1 | 0 | 新建文本文档.txt yes | `08-nearest-2x-try1-raw` (400x32) `08-nearest-2x-try1-prepared` (832x96) |
| 8 | 新建 文本文档.txt | nearest-3x | 1 | 0 | 新建文本文档.txt yes | `08-nearest-3x-try1-raw` (400x32) `08-nearest-3x-try1-prepared` (1232x128) |
| 8 | 新建 文本文档.txt | nearest-4x | 1 | 0 | 新建文本文档.txt yes | `08-nearest-4x-try1-raw` (400x32) `08-nearest-4x-try1-prepared` (1632x160) |
| 8 | 新建 文本文档.txt | bilinear-3x | 1 | 0 | 新建文本文档.txt yes | `08-bilinear-3x-try1-raw` (400x32) `08-bilinear-3x-try1-prepared` (1232x128) |
| 9 | 247 * 119 | raw-1x | 1 | 0 | 247*119 yes | `09-raw-1x-try1-raw` (400x32) `09-raw-1x-try1-prepared` (400x32) |
| 9 | 247 * 119 | nearest-2x | 1 | 0 | 247*119 yes | `09-nearest-2x-try1-raw` (400x32) `09-nearest-2x-try1-prepared` (832x96) |
| 9 | 247 * 119 | nearest-3x | 1 | 0 | 247*119 yes | `09-nearest-3x-try1-raw` (400x32) `09-nearest-3x-try1-prepared` (1232x128) |
| 9 | 247 * 119 | nearest-4x | 1 | 0 | 247*119 yes | `09-nearest-4x-try1-raw` (400x32) `09-nearest-4x-try1-prepared` (1632x160) |
| 9 | 247 * 119 | bilinear-3x | 1 | 0 | 247*119 yes | `09-bilinear-3x-try1-raw` (400x32) `09-bilinear-3x-try1-prepared` (1232x128) |
| 10 | 圆角 阴影 重影 | raw-1x | 1 | 0 | 圆角阴影重影 yes | `10-raw-1x-try1-raw` (400x32) `10-raw-1x-try1-prepared` (400x32) |
| 10 | 圆角 阴影 重影 | nearest-2x | 3 | 0 | 圆角阴影BY **no** | `10-nearest-2x-try1-raw` (400x32) `10-nearest-2x-try1-prepared` (832x96)<br>`10-nearest-2x-try2-raw` (400x32) `10-nearest-2x-try2-prepared` (832x96)<br>`10-nearest-2x-try3-raw` (400x32) `10-nearest-2x-try3-prepared` (832x96) |
| 10 | 圆角 阴影 重影 | nearest-3x | 1 | 0 | 圆角阴影重影 yes | `10-nearest-3x-try1-raw` (400x32) `10-nearest-3x-try1-prepared` (1232x128) |
| 10 | 圆角 阴影 重影 | nearest-4x | 3 | 0 | AA阴影重影 **no** | `10-nearest-4x-try1-raw` (400x32) `10-nearest-4x-try1-prepared` (1632x160)<br>`10-nearest-4x-try2-raw` (400x32) `10-nearest-4x-try2-prepared` (1632x160)<br>`10-nearest-4x-try3-raw` (400x32) `10-nearest-4x-try3-prepared` (1632x160) |
| 10 | 圆角 阴影 重影 | bilinear-3x | 1 | 0 | 圆角阴影重影 yes | `10-bilinear-3x-try1-raw` (400x32) `10-bilinear-3x-try1-prepared` (1232x128) |

## Caret

The same region, read with the text caret inside it. The caret blinks, so consecutive captures
disagree (exit 6), and - measured - a frame with the caret can make the engine misread the whole line.

| frame | caret | read back | conf | screenshot |
| --- | --- | --- | --- | --- |
| frame 1 | see screenshot | 你好，世界 | 84.7 | ``images/caret-frame1.png`` |
| frame 2 | see screenshot | Re,Hh | 49.8 | ``images/caret-frame2.png`` |
| after three newlines (caret below the band) | no | 你好，世界 | 84.7 | |

A band that is too short clips the glyphs and reads as garbage too: 400x20 over the same line gave
`4eim+s` where 400x32 reads it correctly, so the height has to cover the full line box.

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

- exit 0, confidence 47, text `| a@ KeyMouse SSE` (`images/title-bar-raw.png`)

## Files

- `commands.txt` - every command line with exit code, stdout and stderr
- `json/` - the JSON of every call, and the `.err.txt` of every failure
- `images/` - every screenshot, as PNG; the `-raw` files are the pixels as captured
