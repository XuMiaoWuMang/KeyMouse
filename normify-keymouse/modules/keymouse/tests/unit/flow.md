---
uid: 7a1f0c56
id: keymouse.tests.unit.flow
parent: keymouse.tests.unit
tags: [test, unit]
name: {zh: "流程编译与录制规则单测", en: "Flow and recorder unit tests"}
description:
  zh: >
      不需要桌面的那一半：每种步骤编译出的 argv、加载时对未知类型/外来格式/未来版本的拒绝、轨迹抽稀与拖拽判定的边界、虚拟键反查名字，以及"退出码 6 可重试"这条文档承诺。真正的钩子与回放由 smoke 覆盖。
      
  en: >
      The half that needs no desktop: the argv each step compiles to, the loader rejecting unknown types, foreign formats and future versions, the boundaries of trajectory thinning and drag detection, virtual keys mapping back to names, and the documented promise that exit code 6 is retryable. The hooks and the actual replay live in smoke.
      
revision: 8138caf9efa304f3487fcf5c6320ee4ac39f036b
updated_at: "2026-10-03T10:12:03.455Z"
fingerprint: cd3419e243c55c937f13018b39caf1728d6a4afa901607877467c84433b3e83c
source:
  - path: "tests/KeyMouse.Tests/FlowTests.cs"
apis:
  - protocol: rpc
    path: "流程编译与录制规则断言组"
    description:
      zh: >
          步骤→命令、加载校验、抽稀与拖拽。
          
      en: >
          Steps to commands, load validation, thinning and drags.
          
deps:
  - kind: reference
    to: keymouse.flow.model
    to_api: "rpc:FlowDocument.ToArguments"
    label: {zh: "被测的编译器", en: "The compiler under test"}
  - kind: reference
    to: keymouse.record.session
    to_api: "rpc:Recorder.ThinTrajectoryIndices"
    label: {zh: "被测的抽稀规则", en: "The thinning rule"}
---
