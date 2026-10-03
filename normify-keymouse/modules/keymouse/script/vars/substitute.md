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
      
revision: 96c306061d63c034a01c2369e4abc295251e2f35
updated_at: "2026-10-03T13:37:44.319Z"
fingerprint: 460dacfdc9a8f322adac95634e9cc16d4d25d40c8aba413878ef14a5332d4df8
source:
  - path: "src/KeyMouse.Core/ScriptRunner.cs"
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
