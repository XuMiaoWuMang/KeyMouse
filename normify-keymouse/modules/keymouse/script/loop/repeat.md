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
      
revision: e03d53e4c41a8a7220e83123d7f8a44a54ff8dab
updated_at: "2026-10-03T08:15:19.869Z"
fingerprint: 0ef5fdcb1c18d3aaa81d059f6927d97a271aa0ff344d4e213fa1e7d677bd3959
source:
  - path: "ScriptRunner.cs"
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
