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
      
revision: d870b0c5dfd7fe29675863c0288b572a6111a8d4
updated_at: "2026-10-03T07:28:04.457Z"
fingerprint: 0ef5fdcb1c18d3aaa81d059f6927d97a271aa0ff344d4e213fa1e7d677bd3959
source:
  - path: "ScriptRunner.cs"
    line: 452
    end_line: 477
  - path: "ScriptRunner.cs"
    line: 592
    end_line: 634
deps:
  - kind: call
    to: keymouse.script.parse.tokenize
    label: {zh: "逐行分词", en: "Tokenise each line"}
---
