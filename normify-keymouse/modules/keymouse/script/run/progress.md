---
uid: 90a2b4c6
id: keymouse.script.run.progress
parent: keymouse.script.run
tags: [script]
name: {zh: "进度、详情与停止条件", en: "Progress, detail & stop rules"}
description:
  zh: >
      把每次执行写成一行进度（含迭代、继承、重试用时标注）、必要时展开子命令输出，并在首错且未给 --keep-going 时停下；--delay 的间隔也在这里。
      
  en: >
      Writes one progress line per execution (iteration, inherited target and retry notes included), echoes captured output when asked, stops at the first failure unless --keep-going, and applies the inter-command delay.
      
revision: 8138caf9efa304f3487fcf5c6320ee4ac39f036b
updated_at: "2026-10-03T10:11:54.008Z"
fingerprint: f561ff1a777b64b465d886e85545f84beb07f3a3d116e9cb8bb0148ac7caf04c
source:
  - path: "ScriptRunner.cs"
    line: 306
    end_line: 333
apis:
  - protocol: rpc
    path: "进度与详情输出"
    description:
      zh: >
          每条命令一行，失败时展开细节。
          
      en: >
          One line per command, with detail on failure.
          
deps:
  - kind: call
    to: keymouse.script.display
    from_api: "rpc:进度与详情输出"
    to_api: "rpc:ScriptRunner.Quote"
    label: {zh: "回显引号", en: "Quote for display"}
  - kind: call
    to: keymouse.console.width
    from_api: "rpc:进度与详情输出"
    to_api: "rpc:ConsoleText.Pad"
    label: {zh: "宽度对齐", en: "Width alignment"}
---
