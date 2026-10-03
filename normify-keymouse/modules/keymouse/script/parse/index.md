---
uid: ef5f6a7b
id: keymouse.script.parse
parent: keymouse.script
tags: [script, parsing]
name: {zh: "脚本解析", en: "Script parsing"}
description:
  zh: >
      把文本变成可执行的行命令：先分行去注释，再把每行分成 argv。没有第二套命令语言——脚本行的语法就是命令行语法。
      
  en: >
      Turns text into executable lines: split, drop comments, then tokenise each line into argv. There is no second command language - a script line is command-line syntax.
      
revision: 1b316650150f1540368ee548f7d9992ccaa3239e
updated_at: "2026-10-03T11:27:16.005Z"
fingerprint: 77e8c215319806cea2fd833e231759721bc3125357c377795fe231100c8ddc3f
source:
  - path: "src/KeyMouse.Core/ScriptRunner.cs"
    line: 452
    end_line: 477
  - path: "src/KeyMouse.Core/ScriptRunner.cs"
    line: 592
    end_line: 634
deps:
  - kind: call
    to: keymouse.script.parse.tokenize
    label: {zh: "逐行分词", en: "Tokenise each line"}
---
