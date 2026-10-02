---
uid: d7e91b3f
id: keymouse.tests.unit.parsing.invariants.loops
parent: keymouse.tests.unit.parsing.invariants
tags: [test, invariant]
name: {zh: "循环结构用例", en: "Loop structure cases"}
description:
  zh: >
      循环结构分析的断言集：配对、命名、嵌套相乘、repeat 0 跳过、展开计数、超限拒绝、影子变量报错。它护着“结构不合法就在动手前停下”这条不变量。
  en: >
      Assertions for loop analysis: pairing, naming, nested multiplication, repeat 0 skipping the body, expansion counts, refusing an oversized expansion and refusing a shadowed variable.
revision: 8d69164cfe5fa0896f595c0bca51f97977ff51b9
updated_at: "2026-10-02T18:20:00Z"
fingerprint: 8ae0988f099cc0964f5efa1371349b46dff1db459c556cf1cf842f00fbdee247
source:
  - path: "tests/KeyMouse.Tests/ParsingTests.cs"
    line: 22
    end_line: 92
apis:
  - protocol: rpc
    path: "Loops 用例"
    description:
      zh: >
          循环结构与展开计数的断言集。
      en: >
          Assertions for loop structure and expansion counts.
deps:
  - kind: reference
    to: keymouse.script.loop.analyze
    from_api: "rpc:Loops 用例"
    to_api: "rpc:ScriptRunner.AnalyzeLoops"
    label: {zh: "被测对象", en: "Subject"}
  - kind: reference
    to: keymouse.script.loop.repeat
    from_api: "rpc:Loops 用例"
    to_api: "rpc:ScriptRunner.ParseRepeat"
    label: {zh: "循环头语法", en: "Loop header syntax"}
---
