---
uid: 7a1f0d12
id: keymouse.editor.steps
parent: keymouse.editor
tags: [editor, ui]
name: {zh: "步骤视图模型", en: "Step view model"}
description:
  zh: >
      列表与检查器背后的可编辑模型：每个字段直接读写共享 schema 的步骤对象，一次改动同时刷新摘要、JSON 预览与该显示的字段。**每个 setter 先比一次值**——折叠的卡片照样是绑定的，实测打开文件就会把默认值写回模型（click 步骤凭空多出 ms: 0），所以未保存标记按序列化内容比对，而不是"动过手"。
      
  en: >
      The editable model behind the list and the inspector: every field reads and writes the shared schema step directly, and one edit refreshes the summary, the JSON preview and the visible sections together. Every setter compares first - a collapsed card is still bound, and opening a file measurably wrote defaults back (a click step gained ms: 0) - so the unsaved marker compares serialised content.
      
revision: 01b8c3c91b12f7561f62aa9f6af36e7eb9e3bb94
updated_at: "2026-10-03T10:33:31.239Z"
fingerprint: da816a90787059deb4b7e6e2d465f9da0e512bf3514e4187f1923973ec95d12c
source:
  - path: "editor/KeyMouse.FlowEditor/StepVm.cs"
  - path: "editor/KeyMouse.FlowEditor/EditorModel.cs"
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
          按共享 schema 写回文件。
          
      en: >
          Writes the file back through the shared schema.
          
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
