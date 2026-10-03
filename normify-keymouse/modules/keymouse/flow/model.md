---
uid: 7a1f0c54
id: keymouse.flow.model
parent: keymouse.flow
tags: [flow, json]
name: {zh: "流程文档与编译器", en: "Document and compiler"}
description:
  zh: >
      流程的读写与校验：格式名/版本/步骤类型在加载时就拒绝不认识的东西，而不是回放到一半才炸；编译器把一步翻成 argv（客户区坐标 → `-wx/-wy` 加选择器，屏幕坐标 → 绝对坐标，拖拽带时长，组合键与单键分流）。纯函数，可离线单测；客户区坐标没有选择器时直接报错，因为坐标无从换算。
      
  en: >
      Reading, writing and validating a flow: format, version and step types are rejected at load time instead of half-way through a replay, and the compiler turns one step into argv (client coordinates become -wx/-wy plus a selector, screen coordinates stay absolute, a drag carries its duration, combinations and single keys split). Pure and unit-tested; client coordinates without a selector are refused because there is nothing to convert against.
      
revision: 8138caf9efa304f3487fcf5c6320ee4ac39f036b
updated_at: "2026-10-03T10:12:03.455Z"
fingerprint: 8a860c091224e57d74f0b90ed8892f40b2bf1fd8e69e12860546fc2138c06348
source:
  - path: "FlowModel.cs"
apis:
  - protocol: rpc
    path: "FlowDocument.Load"
    description:
      zh: >
          读并校验流程文件（格式、版本、步骤类型）。
          
      en: >
          Loads and validates a flow file (format, version, step types).
          
  - protocol: rpc
    path: "FlowDocument.Save"
    description:
      zh: >
          把流程写成规范化 JSON（录制与编辑器都走它）。
          
      en: >
          Writes the flow as canonical JSON (recorder and editor both use it).
          
  - protocol: rpc
    path: "FlowDocument.ToArguments"
    description:
      zh: >
          把一步编译成命令行参数。
          
      en: >
          Compiles one step into command-line arguments.
          
---
