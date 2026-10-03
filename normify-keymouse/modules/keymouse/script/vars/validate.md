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
      
revision: 8138caf9efa304f3487fcf5c6320ee4ac39f036b
updated_at: "2026-10-03T10:11:54.009Z"
fingerprint: f561ff1a777b64b465d886e85545f84beb07f3a3d116e9cb8bb0148ac7caf04c
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
