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
      
revision: 8138caf9efa304f3487fcf5c6320ee4ac39f036b
updated_at: "2026-10-03T10:11:54.007Z"
fingerprint: f561ff1a777b64b465d886e85545f84beb07f3a3d116e9cb8bb0148ac7caf04c
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
