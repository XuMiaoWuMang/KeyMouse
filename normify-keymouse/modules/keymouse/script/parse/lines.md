---
uid: 0a6b7c8d
id: keymouse.script.parse.lines
parent: keymouse.script.parse
tags: [script, parsing]
name: {zh: "分行与行号", en: "Line splitting"}
description:
  zh: >
      保留每行的原始行号（错误信息要指到源文件的行），跳过空行与 # 注释，并容忍从文档里直接拷出的一行开头带 KeyMouse 前缀。
      
  en: >
      Keeps each line's source number for error messages, skips blanks and comments, and tolerates a leading KeyMouse prefix copied straight out of the docs.
      
revision: 96c306061d63c034a01c2369e4abc295251e2f35
updated_at: "2026-10-03T13:37:44.316Z"
fingerprint: 460dacfdc9a8f322adac95634e9cc16d4d25d40c8aba413878ef14a5332d4df8
source:
  - path: "src/KeyMouse.Core/ScriptRunner.cs"
    line: 452
    end_line: 477
apis:
  - protocol: rpc
    path: "ScriptRunner.Parse"
    description:
      zh: >
          行→（行号, tokens）列表。
          
      en: >
          Lines to numbered token lists.
          
---
