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
      
revision: 6347fbfbc3d7af8839f5dcc9c35501ce2484bdef
updated_at: "2026-10-03T11:11:56.212Z"
fingerprint: ca45bdaf4b9f154a70f5dbccdacf28399554f02a52725ac899164738ebdf95af
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
