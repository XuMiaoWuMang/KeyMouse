---
uid: 7a1f0c56
id: keymouse.tests.unit.flow
parent: keymouse.tests.unit
tags: [test, unit]
name: {zh: "流程编译、谓词与录制规则单测", en: "Flow, predicate and recorder unit tests"}
description:
  zh: >
      不需要桌面的那一半：每种步骤编译出的 argv、加载时对未知类型/外来格式/未来版本/条件缺字段的拒绝、谓词三种模式与去空白规则（含超过栈缓冲区的长串）、轨迹抽稀与拖拽判定的边界、虚拟键反查名字，以及退出码 6 可重试这条文档承诺。契约那一组：每个类型必须有条目、每个字段必须有中文标签且 kind 是界面真能渲染的、契约不许发明加载器不认识的类型，空 when 等于没有前提。
      
  en: >
      The half that needs no desktop: the argv each step compiles to, the loader rejecting unknown types, foreign formats, future versions and incomplete conditions, the three predicate modes and the whitespace rule, trajectory thinning and drag detection, virtual keys mapping back to names, and the contract group (every type has an entry, every field has a label and a renderable kind, no invented types, an empty when means no precondition).
      
revision: ef3cf740e80bd62dd7540df6ee4c526380163233
updated_at: "2026-10-04T14:24:35.936Z"
fingerprint: 967a3e00d9c0f9279a9572e24e28e7c1466377dabac4a6b5c83f31eed4a9b026
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
  - kind: reference
    to: keymouse.flow.schema
    to_api: "rpc:FlowSchema.Steps"
    label: {zh: "被测的契约", en: "The contract under test"}
---
