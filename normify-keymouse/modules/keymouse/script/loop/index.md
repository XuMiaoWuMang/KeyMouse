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
      
revision: 8138caf9efa304f3487fcf5c6320ee4ac39f036b
updated_at: "2026-10-03T10:11:54.006Z"
fingerprint: f561ff1a777b64b465d886e85545f84beb07f3a3d116e9cb8bb0148ac7caf04c
source:
  - path: "ScriptRunner.cs"
    line: 478
    end_line: 591
deps:
  - kind: call
    to: keymouse.script.loop.repeat
    label: {zh: "解析循环头", en: "Parse the loop header"}
---
