---
uid: 7a1f0d12
id: keymouse.editor.steps
parent: keymouse.editor
tags: [editor, ui]
name: {zh: "步骤视图模型与目录", en: "Step view model and catalogue"}
description:
  zh: >
      列表与检查器背后的可编辑模型（StepVm / EditorModel）：每个字段直接读写共享 schema 的步骤对象，一次改动刷新摘要、JSON 预览与可见字段，每个 setter 先比一次值。**显示哪些字段由 `FlowStepSchema` 决定**（不再手写条件），标签按类型说人话（"要找的文字"不是"要输入的文字"），分组/子流程/`read-text`/`when` 都有字段——`when` 是后来补的：以前界面里根本没有它。保存时会**用加载器自检一遍**：编辑器能造出格式会拒绝的东西（半填的 `when` 最容易），这件事该在按下保存的一秒内知道，而不是跑到一半才知道。
      
  en: >
      The editable model behind the list and the inspector (StepVm and EditorModel): every field reads and writes the shared schema step, one edit refreshes the summary, the JSON preview and the visible fields, and every setter compares first. Which fields appear comes from FlowStepSchema rather than hand-written conditions, labels speak per type, and groups, subflows, read-text and when have fields. A save is read back through the loader, so a half-filled when costs a second instead of a failed run.
      
revision: eee5c2b2dedd9f702beb19d6a8286d336b3db1a3
updated_at: "2026-10-03T14:00:46.181Z"
fingerprint: dc1553bbee28cf70dee23c7f93f11af81dff0c2dac7d40082d7627427b98429d
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
