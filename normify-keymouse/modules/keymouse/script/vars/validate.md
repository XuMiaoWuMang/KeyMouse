---
uid: 6a1b2c3d
id: keymouse.script.vars.validate
parent: keymouse.script.vars
tags: [script, variables]
name: {zh: "变量预校验", en: "Variable pre-check"}
description:
  zh: >
      在执行之前把每个 ${name} 对一遍：不在 --set 里、也不在所在循环的变量里，就带行号报错（并提示用 --set）。未闭合的 ${ 同样在这里被抳下。这条保住了“typo 不会让脚本跑到一半才失败”。
      
  en: >
      Checks every ${name} before execution: not from --set and not a loop variable of that line means a line-numbered error. Unterminated ${ is caught here too, which keeps a typo from surfacing halfway through a script.
      
revision: 8d69164cfe5fa0896f595c0bca51f97977ff51b9
updated_at: "2026-10-02T17:37:21.450Z"
fingerprint: 0ef5fdcb1c18d3aaa81d059f6927d97a271aa0ff344d4e213fa1e7d677bd3959
source:
  - path: "ScriptRunner.cs"
    line: 635
    end_line: 673
apis:
  - protocol: rpc
    path: "ScriptRunner.ValidateVariables"
    description:
      zh: >
          执行前校验全部变量引用。
          
      en: >
          Validates every variable reference up front.
          
deps:
  - kind: reference
    to: keymouse.script.loop.analyze
    from_api: "rpc:ScriptRunner.ValidateVariables"
    to_api: "rpc:LoopPlan"
    label: {zh: "查循环作用域", en: "Ask loop scopes"}
---
