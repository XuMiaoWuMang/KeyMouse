---
uid: 4e0f1a2b
id: keymouse.script.loop.repeat
parent: keymouse.script.loop
tags: [script, loop]
name: {zh: "repeat 语法", en: "repeat syntax"}
description:
  zh: >
      循环头规则：次数（0 到 1000000）、可选 as <名字>（默认 i）、名字必须是合法标识符；嵌套时内外重名直接拒绝，而不是让内层默默盖住外层。
      
  en: >
      The header rules: a count from 0 to a million, an optional `as <name>` defaulting to i, names that must be identifiers, and a refusal when nesting would shadow an outer variable instead of silently hiding it.
      
revision: 96c306061d63c034a01c2369e4abc295251e2f35
updated_at: "2026-10-03T13:37:44.316Z"
fingerprint: 460dacfdc9a8f322adac95634e9cc16d4d25d40c8aba413878ef14a5332d4df8
source:
  - path: "src/KeyMouse.Core/ScriptRunner.cs"
    line: 562
    end_line: 591
apis:
  - protocol: rpc
    path: "repeat <次数> as <名字>"
    description:
      zh: >
          循环伪命令语法。
          
      en: >
          The loop pseudo-command syntax.
          
  - protocol: rpc
    path: "ScriptRunner.ParseRepeat"
    description:
      zh: >
          解析循环头（次数+名字+错误）。
          
      en: >
          Parses a loop header.
          
---
