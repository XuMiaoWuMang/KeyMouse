---
uid: 7b2c3d4e
id: keymouse.script.vars.substitute
parent: keymouse.script.vars
tags: [script, variables]
name: {zh: "${} 替换", en: "${} substitution"}
description:
  zh: >
      在单个 token 内替换全部 ${name}（同一 token 可引用多次）；值原样插入，不再二次扫描，所以值里出现 ${...} 也不会被当成变量。
      
  en: >
      Replaces every ${name} inside one token, any number of times; the value is inserted literally and never rescanned, so a value containing ${...} is not treated as a variable.
      
revision: d870b0c5dfd7fe29675863c0288b572a6111a8d4
updated_at: "2026-10-03T07:28:04.461Z"
fingerprint: 0ef5fdcb1c18d3aaa81d059f6927d97a271aa0ff344d4e213fa1e7d677bd3959
source:
  - path: "ScriptRunner.cs"
    line: 674
    end_line: 716
apis:
  - protocol: rpc
    path: "ScriptRunner.Substitute"
    description:
      zh: >
          单 token 变量替换。
          
      en: >
          Substitutes variables in one token.
          
---
