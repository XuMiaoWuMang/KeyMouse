---
uid: 1b7c8d9e
id: keymouse.script.parse.tokenize
parent: keymouse.script.parse
tags: [script, parsing]
name: {zh: "分词", en: "Tokenising"}
description:
  zh: >
      一行 → argv：双引号分组、\" 与 \\ 转义、# 行内注释、空行忽略。引号规则故意与命令行一致，学了就不会用错。
      
  en: >
      One line to argv: double quotes group, backslash escapes, trailing comments, blanks ignored - deliberately the same quoting rules as the command line.
      
revision: 6347fbfbc3d7af8839f5dcc9c35501ce2484bdef
updated_at: "2026-10-03T11:11:56.209Z"
fingerprint: ca45bdaf4b9f154a70f5dbccdacf28399554f02a52725ac899164738ebdf95af
source:
  - path: "src/KeyMouse.Core/ScriptRunner.cs"
    line: 592
    end_line: 634
apis:
  - protocol: rpc
    path: "ScriptRunner.Tokenize"
    description:
      zh: >
          一行文本→参数数组。
          
      en: >
          One line of text to an argv array.
          
---
