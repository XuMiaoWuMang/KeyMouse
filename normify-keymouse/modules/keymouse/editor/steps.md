---
uid: 7a1f0d12
id: keymouse.editor.steps
parent: keymouse.editor
tags: [editor, ui]
name: {zh: "步骤视图模型与目录", en: "Step view model and catalogue"}
description:
  zh: >
      列表与检查器背后的可编辑模型：每个字段直接读写共享 schema 的步骤对象，一次改动刷新摘要、JSON 预览与可见字段；每个 setter 先比一次值（折叠卡片也会回写默认值，实测过）。分组（`repeat`/`foreach`）与 `read-text` 在这里有摘要与字段；**子步骤原样保留**（视图模型包的是整对象，保存不会丢循环体）。
      
  en: >
      The editable model behind the list and the inspector: every field reads and writes the shared schema step, one edit refreshes the summary, the JSON preview and the visible fields, and every setter compares first (collapsed cards do write defaults back - measured). Groups and read-text get their own summaries and fields, and child steps are preserved verbatim: the view model wraps whole objects, so saving never loses a loop body.
      
revision: 1ebae14ff2430b597cc4a1695a71ddf788879db1
updated_at: "2026-10-03T12:49:24.519Z"
fingerprint: 7f785b14625f81571c79580f2f3eaa14957572be6879d17a5367e6586f9b46d5
source:
  - path: "editor/KeyMouse.FlowEditor/StepVm.cs"
  - path: "editor/KeyMouse.FlowEditor/EditorModel.cs"
  - path: "editor/KeyMouse.FlowEditor/Compat.cs"
apis:
  - protocol: rpc
    path: "EditorModel.Load"
    description:
      zh: >
          读入流程文件，失败时带着加载器的原话。
          
      en: >
          Loads a flow file, carrying the loader own message on failure.
          
  - protocol: rpc
    path: "EditorModel.Save"
    description:
      zh: >
          按共享 schema 写回文件（保留分组子步骤）。
          
      en: >
          Writes the file back through the shared schema (group children included).
          
  - protocol: rpc
    path: "EditorModel.MarkDirty"
    description:
      zh: >
          按内容判断是否有未保存改动。
          
      en: >
          Decides "unsaved" by comparing content.
          
  - protocol: rpc
    path: "StepVm.Edit"
    description:
      zh: >
          值没变就什么都不做的写入守卫。
          
      en: >
          The write guard that ignores a no-op value.
          
deps:
  - kind: reference
    to: keymouse.flow.model
    to_api: "rpc:FlowDocument.Load"
    label: {zh: "同一份 schema", en: "The same schema"}
  - kind: reference
    to: keymouse.flow.model
    to_api: "rpc:FlowDocument.Save"
    label: {zh: "写回也是它", en: "And for writing back"}
---
