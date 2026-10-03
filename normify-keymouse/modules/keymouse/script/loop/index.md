---
uid: 2c8d9e0f
id: keymouse.script.loop
parent: keymouse.script
tags: [script, loop]
name: {zh: "循环", en: "Loops"}
description:
  zh: >
      repeat <次数> [as <名字>] … end：循环变量从 0 开始。结构在执行前全部分析完（配对/上限/重名/展开总数），执行时用程序计数器+循环栈展开，不是把脚本复制成多份。
      
  en: >
      repeat <n> [as <name>] ... end, with a 0-based loop variable. The whole structure is analysed before execution; at runtime a program counter and a loop stack expand it instead of rewriting the script.
      
revision: 1b316650150f1540368ee548f7d9992ccaa3239e
updated_at: "2026-10-03T11:27:16.004Z"
fingerprint: 77e8c215319806cea2fd833e231759721bc3125357c377795fe231100c8ddc3f
source:
  - path: "src/KeyMouse.Core/ScriptRunner.cs"
    line: 478
    end_line: 591
deps:
  - kind: call
    to: keymouse.script.loop.repeat
    label: {zh: "解析循环头", en: "Parse the loop header"}
---
