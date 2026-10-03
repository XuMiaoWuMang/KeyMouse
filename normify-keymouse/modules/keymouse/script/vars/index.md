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
      
revision: 8138caf9efa304f3487fcf5c6320ee4ac39f036b
updated_at: "2026-10-03T10:11:54.009Z"
fingerprint: f561ff1a777b64b465d886e85545f84beb07f3a3d116e9cb8bb0148ac7caf04c
source:
  - path: "ScriptRunner.cs"
    line: 635
    end_line: 716
---
