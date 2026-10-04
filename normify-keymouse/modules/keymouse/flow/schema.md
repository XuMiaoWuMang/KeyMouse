---
uid: 7a1f4001
id: keymouse.flow.schema
parent: keymouse.flow
tags: [flow, core, contract]
name: {zh: "流程格式契约", en: "Flow format contract"}
description:
  zh: >
      流程格式的接口契约：一个 JSON 文件（随程序集嵌入）说清每种步骤有哪些字段、哪些必填、范围与枚举的中文标签，FlowSchema 只负责读它。加载器与编辑器都问同一份：界面里不再有第二张"显示什么"的表。验证由测试盯着——每个类型必须有条目、每个字段必须有中文标签、kind 必须是界面真渲染得出的那些，反过来契约也不许发明加载器不认识的类型。
      
  en: >
      The flow format's interface contract: one JSON file (embedded in the assembly) declaring every step type's fields, which are required, their ranges and the Chinese labels for enum values; FlowSchema only reads it. Loader and editor ask the same file, so the UI holds no second table of what to show. Tests enforce it: every type has an entry, every field has a label and a kind the editor can render, and the contract may not invent types the loader rejects.
      
revision: ef3cf740e80bd62dd7540df6ee4c526380163233
updated_at: "2026-10-04T14:24:35.929Z"
fingerprint: 7405a24a848226ca93e6b347413a4bf10bc5ae6044a2040a96f52ddedee7db08
source:
  - path: "src/KeyMouse.Core/flow.schema.json"
  - path: "src/KeyMouse.Core/FlowSchema.cs"
apis:
  - protocol: file
    path: "src/KeyMouse.Core/flow.schema.json"
    description:
      zh: >
          契约本体（机器可读的那份，随程序集嵌入）。
          
      en: >
          The contract itself (the machine-readable one, embedded in the assembly).
          
  - protocol: rpc
    path: "FlowSchema.Steps"
    description:
      zh: >
          按步骤类型索引的契约表：字段、必填、范围、枚举中文标签。
          
      en: >
          Entries by step type: fields, required, ranges, Chinese enum labels.
          
  - protocol: rpc
    path: "FlowSchema.TryGetStep"
    description:
      zh: >
          某个步骤类型的契约（不认识就 false）。
          
      en: >
          The contract of one step type (false when the type is unknown).
          
  - protocol: rpc
    path: "FlowSchema.ShapeFields"
    description:
      zh: >
          形状（region / point / target / when 等）内部的字段。
          
      en: >
          The inner fields of a shape such as region, point, target or when.
          
  - protocol: rpc
    path: "FlowSchema.FieldsFor"
    description:
      zh: >
          某个步骤类型要渲染的字段，按阅读顺序。
          
      en: >
          The fields to render for a step type, in reading order.
          
---
