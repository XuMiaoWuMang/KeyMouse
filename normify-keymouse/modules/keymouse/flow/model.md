---
uid: 7a1f0c54
id: keymouse.flow.model
parent: keymouse.flow
tags: [flow, json]
name: {zh: "流程文档与编译器", en: "Document and compiler"}
description:
  zh: >
      流程的读写与校验：格式名/版本/步骤类型在加载时就拒绝不认识的东西；条件、前置条件、循环与变量同样在加载时校验（region/text/match 规则、`repeat` 的 times 上限、`foreach` 的列表必须存在、分组不能带 when、`{{占位符}}` 必须在作用域里且 `read-text` 的 into 对后续步骤可见）。另有展平（循环 → 步骤）与替换（`Resolve` 每步一次）。
      
  en: >
      Reading, writing and validating a flow: format, version and step types are rejected at load time, and so are conditions, preconditions, loops and variables (the region/text/match rules, the times cap, a foreach list that must exist, groups that cannot carry a when, placeholders that must be in scope, and a read-text into that is visible to later steps). It also flattens loops into steps and resolves placeholders per step.
      
revision: 69321dbdf0d2f94c72a77144dd1c35e063d13815
updated_at: "2026-10-03T11:41:09.822Z"
fingerprint: 8b6008aead66352daab982f3f00c2249404df7c57de7d6f400753c550d97bb8e
source:
  - path: "src/KeyMouse.Core/FlowModel.cs"
  - path: "src/KeyMouse.Core/TextPredicate.cs"
apis:
  - protocol: rpc
    path: "FlowDocument.Load"
    description:
      zh: >
          读并校验（含条件、循环与变量作用域）。
          
      en: >
          Loads and validates (conditions, loops and variable scope included).
          
  - protocol: rpc
    path: "FlowDocument.Save"
    description:
      zh: >
          把流程写成规范化 JSON（录制与编辑器都走它）。
          
      en: >
          Writes the flow as canonical JSON (recorder and editor both use it).
          
  - protocol: rpc
    path: "FlowDocument.ExpandLoops"
    description:
      zh: >
          把 repeat/foreach 展平成它们会执行的步骤。
          
      en: >
          Flattens repeat/foreach into the steps they would run.
          
  - protocol: rpc
    path: "FlowDocument.Resolve"
    description:
      zh: >
          派发前解析一步里的 {{占位符}}。
          
      en: >
          Resolves the placeholders of one step before dispatch.
          
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
