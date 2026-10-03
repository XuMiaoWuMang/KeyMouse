---
uid: 7a1f0c56
id: keymouse.tests.unit.flow
parent: keymouse.tests.unit
tags: [test, unit]
name: {zh: "流程编译、谓词与录制规则单测", en: "Flow, predicate and recorder unit tests"}
description:
  zh: >
      不需要桌面的那一半：每种步骤编译出的 argv、加载时对未知类型/外来格式/未来版本/条件缺字段的拒绝、谓词三种模式与去空白规则（含超过栈缓冲区的长串）、轨迹抽稀与拖拽判定的边界、虚拟键反查名字，以及"退出码 6 可重试"这条文档承诺。真正的钩子、OCR 与回放由 smoke 覆盖。
      
  en: >
      The half that needs no desktop: the argv each step compiles to, the loader rejecting unknown types, foreign formats, future versions and incomplete conditions, the three predicate modes and the whitespace rule (including a string past the stack buffer), the boundaries of trajectory thinning and drag detection, virtual keys mapping back to names, and the documented promise that exit 6 is retryable. The hooks, the OCR and the replay live in smoke.
      
revision: eee5c2b2dedd9f702beb19d6a8286d336b3db1a3
updated_at: "2026-10-03T14:00:46.182Z"
fingerprint: e2b7d660e8c1526b5765587dfad138dac818dab5c8fe0eb0ac2b8b6d543c7c44
source:
  - path: "tests/KeyMouse.Tests/FlowTests.cs"
apis:
  - protocol: rpc
    path: "流程、谓词与录制规则断言组"
    description:
      zh: >
          步骤→命令、加载校验、谓词模式、抽稀与拖拽。
          
      en: >
          Steps to commands, load validation, predicate modes, thinning and drags.
          
deps:
  - kind: reference
    to: keymouse.flow.model
    to_api: "rpc:FlowDocument.ToArguments"
    label: {zh: "被测的编译器", en: "The compiler under test"}
  - kind: reference
    to: keymouse.flow.predicate
    to_api: "rpc:TextPredicate.Matches"
    label: {zh: "被测的谓词", en: "The predicate under test"}
  - kind: reference
    to: keymouse.record.session
    to_api: "rpc:Recorder.ThinTrajectoryIndices"
    label: {zh: "被测的抽稀规则", en: "The thinning rule"}
---
