---
uid: 5f1a2b3c
id: keymouse.script.vars
parent: keymouse.script
tags: [script, variables]
name: {zh: "变量", en: "Variables"}
description:
  zh: >
      ${name} 的替换与预校验：变量来自 --set 或所在循环的循环变量；替换发生在分词之后，所以带空格的值仍是一个参数。
      
  en: >
      ${name} substitution and its upfront check: a name comes from --set or from a loop that encloses the line. Substitution happens after tokenising, so a value with spaces stays one argument.
      
revision: e03d53e4c41a8a7220e83123d7f8a44a54ff8dab
updated_at: "2026-10-03T08:15:19.875Z"
fingerprint: 0ef5fdcb1c18d3aaa81d059f6927d97a271aa0ff344d4e213fa1e7d677bd3959
source:
  - path: "ScriptRunner.cs"
    line: 635
    end_line: 716
---
