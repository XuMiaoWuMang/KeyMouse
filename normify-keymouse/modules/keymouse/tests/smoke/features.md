---
uid: 4b5d6f71
id: keymouse.tests.smoke.features
parent: keymouse.tests.smoke
tags: [test, desktop]
name: {zh: "逃生口、stdin、keep-going、inspect", en: "Hatch, stdin, keep-going, inspect"}
description:
  zh: >
      零散入口的覆盖：vk: 裸虚拟键（曾从 v1.0 起就写在文档里却从未工作）、从管道读脚本、--keep-going 与报告、window inspect 的判定文案与退出码、客户区坐标移动后光标是否真在那里。
      
  en: >
      Cover for the odds and ends: the raw vk: key (documented since v1.0 yet broken until a test caught it), script from a pipe, --keep-going with a report, inspect's verdict text and exit codes, and whether a client-relative move really puts the cursor there.
      
revision: 1b316650150f1540368ee548f7d9992ccaa3239e
updated_at: "2026-10-03T11:27:16.010Z"
fingerprint: 2ab5b1e29a47f02cf1bf9588824895c65511b838f298921b03517b68cf0c2879
source:
  - path: "tests/smoke.ps1"
    line: 216
    end_line: 289
apis:
  - protocol: rpc
    path: "零散入口断言组"
    description:
      zh: >
          逃生口、stdin、keep-going、inspect。
          
      en: >
          Hatch, stdin, keep-going, inspect.
          
deps:
  - kind: call
    to: keymouse.cli.main
    from_api: "rpc:零散入口断言组"
    to_api: "rpc:keymouse <命令组>"
    label: {zh: "驱动被测命令", en: "Drive the command"}
---
