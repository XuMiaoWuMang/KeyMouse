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
      
revision: d870b0c5dfd7fe29675863c0288b572a6111a8d4
updated_at: "2026-10-03T07:28:04.458Z"
fingerprint: 0ef5fdcb1c18d3aaa81d059f6927d97a271aa0ff344d4e213fa1e7d677bd3959
source:
  - path: "ScriptRunner.cs"
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
