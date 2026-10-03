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
      
revision: 96c306061d63c034a01c2369e4abc295251e2f35
updated_at: "2026-10-03T13:37:44.317Z"
fingerprint: 460dacfdc9a8f322adac95634e9cc16d4d25d40c8aba413878ef14a5332d4df8
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
