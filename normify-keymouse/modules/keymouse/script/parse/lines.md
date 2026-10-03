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
      
revision: 1b316650150f1540368ee548f7d9992ccaa3239e
updated_at: "2026-10-03T11:27:16.005Z"
fingerprint: 77e8c215319806cea2fd833e231759721bc3125357c377795fe231100c8ddc3f
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
