---
uid: e8fa2c40
id: keymouse.tests.unit.parsing.invariants.inheritance
parent: keymouse.tests.unit.parsing.invariants
tags: [test, invariant]
name: {zh: "目标继承用例", en: "Target inheritance cases"}
description:
  zh: >
      继承规则的断言集：选择器识别（含 --flag=value 与多条件）、哪些命令继承（mouse/key）与哪些刻意不继承（waitfor / waitgone / window list）、客户区坐标不算目标。
      
  en: >
      Assertions for the inheritance rules: selector extraction (inline values, several conditions), which commands inherit (mouse and key) and which deliberately do not (waitfor, waitgone, window list).
      
revision: 96c306061d63c034a01c2369e4abc295251e2f35
updated_at: "2026-10-03T13:37:44.321Z"
fingerprint: 1263671f94fb9ec06ca4072a554642c69297a717af171eff59c55c582848dc59
source:
  - path: "tests/KeyMouse.Tests/ParsingTests.cs"
    line: 139
    end_line: 169
apis:
  - protocol: rpc
    path: "TargetInheritance 用例"
    description:
      zh: >
          目标继承规则的断言集。
          
      en: >
          Assertions for the inheritance rules.
          
deps:
  - kind: reference
    to: keymouse.script.target.extract
    from_api: "rpc:TargetInheritance 用例"
    to_api: "rpc:ScriptRunner.ExtractTargetTokens"
    label: {zh: "选择器提取", en: "Selector extraction"}
  - kind: reference
    to: keymouse.script.target.inherit
    from_api: "rpc:TargetInheritance 用例"
    to_api: "rpc:ScriptRunner.InheritsTarget"
    label: {zh: "继承规则", en: "Inheritance rule"}
---
