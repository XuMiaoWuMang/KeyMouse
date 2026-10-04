---
uid: 7a1f0c54
id: keymouse.flow.model
parent: keymouse.flow
tags: [flow, json]
name: {zh: "流程文档与编译器", en: "Document and compiler"}
description:
  zh: >
      流程的读写与校验：格式名/版本/步骤类型在加载时就拒绝不认识的东西；条件、前置条件、循环、变量与 call 同样在加载时校验（region/text/match 规则、times 上限、foreach 的列表必须存在、分组不能带 when、占位符必须在作用域里、into 与 export 对后续步骤可见、子流程必须存在且不成环）。字段必填由契约（FlowSchema）判定；when 里字段全空等于没有前提，不该因此作废。allowRestore 是同意开关：默认关，还原别人的最小化窗口要明说。
      
  en: >
      Reading, writing and validating a flow: format, version and step types are rejected at load time, and so are conditions, loops, variables and calls (region/text/match rules, the times cap, foreach lists, groups without when, placeholders in scope, into/export visibility, subflow cycles). Required fields come from the contract (FlowSchema), an all-empty when means no precondition, and allowRestore carries the consent to bring a minimized window back.
      
revision: ef3cf740e80bd62dd7540df6ee4c526380163233
updated_at: "2026-10-04T14:24:35.929Z"
fingerprint: 5955e9ecd0e3cc23f5f4d39909cd12cb391ccb4631e8617ef8114b35197c339a
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
  - kind: reference
    to: keymouse.flow.schema
    from_api: "rpc:FlowDocument.Load"
    to_api: "rpc:FlowSchema.TryGetStep"
    label: {zh: "必填由它判定", en: "Required fields from it"}
---
