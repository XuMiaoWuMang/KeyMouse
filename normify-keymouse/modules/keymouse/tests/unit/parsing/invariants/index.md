---
uid: f6081a2c
id: keymouse.tests.unit.parsing.invariants
parent: keymouse.tests.unit.parsing
tags: [test]
name: {zh: "循环与目标继承用例", en: "Loops & target inheritance"}
description:
  zh: >
      循环结构分析的断言集（配对、命名、展开计数、超限拒绝、影子变量）与目标继承规则（哪些命令继承、哪些刻意不继承）。这两组最直接护着 v2 的新语义。
      
  en: >
      Loop analysis (pairing, naming, expansion counts, refusing an oversized expansion, shadowing) and the inheritance rules - which commands inherit and which deliberately do not. These two sets protect the v2 semantics.
      
revision: 6347fbfbc3d7af8839f5dcc9c35501ce2484bdef
updated_at: "2026-10-03T11:11:56.215Z"
fingerprint: 36327f9a4054c210af82c13d5b23bc44d4e5d8defd5bf0f6e966adab5530ed35
source:
  - path: "tests/KeyMouse.Tests/ParsingTests.cs"
    line: 22
    end_line: 92
  - path: "tests/KeyMouse.Tests/ParsingTests.cs"
    line: 139
    end_line: 169
---
