---
uid: 7a1f0c54
id: keymouse.flow.model
parent: keymouse.flow
tags: [flow, json]
name: {zh: "流程文档与编译器", en: "Document and compiler"}
description:
  zh: >
      流程的读写与校验：格式名/版本/步骤类型在加载时就拒绝不认识的东西，而不是回放到一半才炸；条件步骤也在加载时校验（`wait-text` 必须有 region 与 text、match 必须是已知模式、region 只能是客户区坐标），让一个拼写错误只花一秒钟而不是等满超时。编译器把一步翻成 argv，客户区坐标没有选择器时直接报错，因为坐标无从换算。
      
  en: >
      Reading, writing and validating a flow: format, version and step types are rejected at load time instead of half-way through a replay, and conditions are validated there too (a wait-text must carry a region and text, its match must be a known mode, its region must be client-relative), so a typo costs a second instead of a full timeout. The compiler turns a step into argv and refuses client coordinates without a selector, because there is nothing to convert against.
      
revision: 89f30aa8db66e03f9c60253d85abc64d7760319e
updated_at: "2026-10-03T10:20:36.520Z"
fingerprint: 287793196d347ecb59faae080d78a191ff61e10e1383430f7c651bfca3f73a37
source:
  - path: "FlowModel.cs"
  - path: "TextPredicate.cs"
apis:
  - protocol: rpc
    path: "FlowDocument.Load"
    description:
      zh: >
          读并校验流程文件（格式、版本、步骤类型、条件字段）。
          
      en: >
          Loads and validates a flow file (format, version, step types, condition fields).
          
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
          
deps:
  - kind: reference
    to: keymouse.flow.predicate
    to_api: "rpc:TextPredicate.IsKnownMode"
    label: {zh: "加载时校验匹配方式", en: "Validate the mode at load time"}
---
