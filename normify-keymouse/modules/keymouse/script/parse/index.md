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
      
revision: 96c306061d63c034a01c2369e4abc295251e2f35
updated_at: "2026-10-03T13:37:44.316Z"
fingerprint: 460dacfdc9a8f322adac95634e9cc16d4d25d40c8aba413878ef14a5332d4df8
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
