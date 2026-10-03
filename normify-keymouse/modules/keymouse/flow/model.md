---
uid: 7a1f0c54
id: keymouse.flow.model
parent: keymouse.flow
tags: [flow, json]
name: {zh: "流程文档与编译器", en: "Document and compiler"}
description:
  zh: >
      流程的读写与校验：格式名/版本/步骤类型在加载时就拒绝不认识的东西，而不是回放到一半才炸；条件与前置条件也在加载时校验（`wait-text`/`click-text` 必须有 region 与 text、match 必须是已知模式、region 只能客户区坐标；`when` 还要能定位到窗口、`else` 只能是 skip/fail），让拼写错误只花一秒钟而不是等满超时。
      
  en: >
      Reading, writing and validating a flow: format, version and step types are rejected at load time instead of half-way through a replay, and conditions and preconditions are validated there too (wait-text/click-text need a region and text, the match must be a known mode, the region must be client-relative; a when must resolve to a window and its else must be skip or fail), so a typo costs a second instead of a full timeout.
      
revision: 1b316650150f1540368ee548f7d9992ccaa3239e
updated_at: "2026-10-03T11:27:19.954Z"
fingerprint: 2838732592244726e1a4d2c3931cfd691fb7099e74da3a730762eb47b3598eca
source:
  - path: "src/KeyMouse.Core/FlowModel.cs"
  - path: "src/KeyMouse.Core/TextPredicate.cs"
apis:
  - protocol: rpc
    path: "FlowDocument.Load"
    description:
      zh: >
          读并校验流程文件（格式、版本、步骤类型、条件与前置条件）。
          
      en: >
          Loads and validates a flow file (format, version, step types, conditions and preconditions).
          
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
    label: {zh: "加载时校验匹配方式", en: "Validate mode at load"}
---
