---
uid: 7a1f4001
id: keymouse.flow.schema
parent: keymouse.flow
tags: [flow, core]
name: {zh: "步骤字段表", en: "Step field schema"}
description:
  zh: >
      哪种步骤用哪些字段——一张只有格式自己维护的表。检查器不再手写"显示什么"的条件：编辑器问它，于是加类型或加字段时改的是同一处，界面不会悄悄漏掉参数（曾经漏过：`click-text` 在界面上只剩目标与备注，区域、文字、匹配、上限、按钮全部藏着）。一个单元测试遍历 `KnownTypes`，拒绝任何没有声明的类型，并钉住 `click-text`/`read-text`/`wait-text` 必须带区域、文字、匹配、上限这些字段。
      
  en: >
      Which step type uses which fields - one table the format itself maintains. The inspector no longer hand-writes hide/show conditions: it asks this table, so a type or a field changes in one place and the UI cannot silently lose parameters (it did: click-text showed only a target and a note while its region, text, match mode, timeout and button stayed hidden). A unit test walks KnownTypes and refuses a type without an entry.
      
revision: eee5c2b2dedd9f702beb19d6a8286d336b3db1a3
updated_at: "2026-10-03T14:00:46.180Z"
fingerprint: bb5056a358f7c4ac4b6eaae616d1bd0bfc72b1e85d9f1916dbeb7deac6ce251f
source:
  - path: "src/KeyMouse.Core/FlowStepSchema.cs"
apis:
  - protocol: rpc
    path: "FlowStepSchema.For"
    description:
      zh: >
          某个步骤类型用到哪些字段（位标志）。
          
      en: >
          Which fields a step type uses (a flag set).
          
---
