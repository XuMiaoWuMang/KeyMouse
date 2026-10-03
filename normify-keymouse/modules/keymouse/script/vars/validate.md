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
      
revision: 6347fbfbc3d7af8839f5dcc9c35501ce2484bdef
updated_at: "2026-10-03T11:11:56.212Z"
fingerprint: ca45bdaf4b9f154a70f5dbccdacf28399554f02a52725ac899164738ebdf95af
source:
  - path: "src/KeyMouse.Core/ScriptRunner.cs"
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
