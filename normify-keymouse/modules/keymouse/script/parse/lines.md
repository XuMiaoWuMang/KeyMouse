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
      
revision: d870b0c5dfd7fe29675863c0288b572a6111a8d4
updated_at: "2026-10-03T07:28:04.458Z"
fingerprint: 0ef5fdcb1c18d3aaa81d059f6927d97a271aa0ff344d4e213fa1e7d677bd3959
source:
  - path: "ScriptRunner.cs"
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
