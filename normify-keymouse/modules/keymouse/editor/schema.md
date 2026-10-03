---
uid: 7a1f0d14
id: keymouse.editor.schema
parent: keymouse.editor
tags: [editor, schema]
name: {zh: "共享 schema 链接", en: "Linked shared schema"}
description:
  zh: >
      编辑器不重写流程格式：FlowModel.cs 与 TextPredicate.cs 通过 `<Compile Include="..\..\" />` 原样编进编辑器程序集，只补一个 10 行的 CommandFailure 兼容声明（与原版同形：一个退出码 + 一句话）。代价是根项目必须 `<Compile Remove="editor/**" />`，否则会把编辑器整个编进控制台程序（实测：重复 AssemblyInfo 特性）。
      
  en: >
      The editor does not re-implement the flow format: FlowModel.cs and TextPredicate.cs are compiled in verbatim via `<Compile Include="..\..\" />`, with only a ten-line CommandFailure declaration to match (a code and a message). The price: the root project must `<Compile Remove="editor/**" />`, or it compiles the editor into the console app (measured: duplicate AssemblyInfo attributes).
      
revision: 01b8c3c91b12f7561f62aa9f6af36e7eb9e3bb94
updated_at: "2026-10-03T10:33:31.240Z"
fingerprint: e6fa3130a76c69103863f80e7d713dcb26987b89b3bfa5eb406132dd6c7a001d
source:
  - path: "editor/KeyMouse.FlowEditor/CommandFailure.cs"
  - path: "editor/KeyMouse.FlowEditor/Compat.cs"
apis:
  - protocol: file
    path: "FlowModel.cs"
    description:
      zh: >
          原样链接进来的流程文档模型与编译器（编辑器的读写都走它）。
          
      en: >
          The flow document model and compiler, linked verbatim (all editor reads and writes go through it).
          
  - protocol: file
    path: "TextPredicate.cs"
    description:
      zh: >
          原样链接进来的匹配模式定义（检查器里那个下拉框就是它）。
          
      en: >
          The match-mode definitions, linked verbatim (the inspector dropdown is this).
          
  - protocol: file
    path: "CommandFailure.cs"
    description:
      zh: >
          10 行兼容声明：与原版同形（一个退出码 + 一句话）。
          
      en: >
          The ten-line compatible declaration: same shape as the original (a code and a message).
          
deps:
  - kind: reference
    to: keymouse.flow.model
    label: {zh: "同一份文档模型", en: "The same document model"}
  - kind: reference
    to: keymouse.flow.predicate
    label: {zh: "同一套匹配方式", en: "The same match modes"}
---
