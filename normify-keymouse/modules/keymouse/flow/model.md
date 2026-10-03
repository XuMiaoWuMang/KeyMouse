---
uid: 7a1f0c54
id: keymouse.flow.model
parent: keymouse.flow
tags: [flow, json]
name: {zh: "流程文档与编译器", en: "Document and compiler"}
description:
  zh: >
      流程的读写与校验：格式名/版本/步骤类型在加载时就拒绝不认识的东西；条件、前置条件、循环、变量与 `call` 同样在加载时校验（region/text/match 规则、times 上限、foreach 的列表必须存在、分组不能带 when、`{{占位符}}` 必须在作用域里、`read-text` 的 into 与 `call` 的 export 对后续步骤可见、子流程必须存在且不成环）。另负责按帧解析。分组只检查自己的字段：子步骤在它们自己的作用域里单独校验。
      
  en: >
      Validating a flow at load time: format, version, step types, conditions, preconditions, loops, variables and calls - including the region/text/match rules, the times cap, a foreach list that must exist, a group that cannot carry a when, placeholders that must be in scope, a read-text into and a call export later steps may use, and a subflow that must exist without cycles. Placeholders resolve against a frame, and a group is checked only for the placeholders in its own fields.
      
revision: 1ebae14ff2430b597cc4a1695a71ddf788879db1
updated_at: "2026-10-03T12:49:24.517Z"
fingerprint: 553948866419b34f39c26b888689b5bf2ff249b451583c560599296f43adcfb1
source:
  - path: "src/KeyMouse.Core/FlowModel.cs"
  - path: "src/KeyMouse.Core/TextPredicate.cs"
apis:
  - protocol: rpc
    path: "FlowDocument.Load"
    description:
      zh: >
          读并校验（条件、循环、变量与 call）。
          
      en: >
          Loads and validates (conditions, loops, variables and calls).
          
  - protocol: rpc
    path: "FlowDocument.Save"
    description:
      zh: >
          把流程写成规范化 JSON（录制与编辑器都走它）。
          
      en: >
          Writes the flow as canonical JSON (recorder and editor both use it).
          
  - protocol: rpc
    path: "FlowDocument.Resolve"
    description:
      zh: >
          派发前按帧解析一步里的占位符。
          
      en: >
          Resolves a step placeholders against its frame before dispatch.
          
  - protocol: rpc
    path: "FlowDocument.Expand"
    description:
      zh: >
          按帧替换一段文本里的占位符（vars 也用它）。
          
      en: >
          Expands placeholders in a string against a frame (vars uses it too).
          
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
