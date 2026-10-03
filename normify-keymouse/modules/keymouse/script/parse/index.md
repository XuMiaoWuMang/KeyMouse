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
      
revision: 6347fbfbc3d7af8839f5dcc9c35501ce2484bdef
updated_at: "2026-10-03T11:11:56.208Z"
fingerprint: ca45bdaf4b9f154a70f5dbccdacf28399554f02a52725ac899164738ebdf95af
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
