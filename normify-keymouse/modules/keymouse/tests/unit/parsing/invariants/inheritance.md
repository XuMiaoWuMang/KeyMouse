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
revision: 8d69164cfe5fa0896f595c0bca51f97977ff51b9
updated_at: "2026-10-02T18:20:00Z"
fingerprint: 8ae0988f099cc0964f5efa1371349b46dff1db459c556cf1cf842f00fbdee247
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
