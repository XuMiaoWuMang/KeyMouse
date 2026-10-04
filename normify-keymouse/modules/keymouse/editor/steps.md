---
uid: 7a1f0d12
id: keymouse.editor.steps
parent: keymouse.editor
tags: [editor, ui]
name: {zh: "步骤视图模型与目录", en: "Step view model and catalogue"}
description:
  zh: >
      列表与检查器背后的可编辑模型（StepVm / EditorModel）：每个字段直接读写共享 schema 的步骤对象，一次改动刷新摘要、JSON 预览与可见字段，每个 setter 先比一次值。显示哪些字段由契约（FlowSchema）决定，标签按类型说人话，when 也是契约的一部分。保存时用加载器自检一遍：编辑器能造出格式会拒绝的东西（半填的 when 最容易），这件事该在按下保存的一秒内知道。行号与选中态是逐行通知的——x:Bind 默认一次性，不逐个发就不重画（实测：中间插一步后重号 1,2,3,3）。
      
  en: >
      The editable model behind the list and the inspector (StepVm and EditorModel): every field reads and writes the shared schema step, and one edit refreshes the summary, the JSON preview and the visible fields. Which fields appear comes from the contract (FlowSchema). A save is read back through the loader, so a half-filled when costs a second instead of a failed run. Row number and selected state notify per row, because x:Bind is one-time by default.
      
revision: ef3cf740e80bd62dd7540df6ee4c526380163233
updated_at: "2026-10-04T14:24:35.932Z"
fingerprint: 3e487fadf8de240d4a1106d5746957ccd8f516db6adfd93da123f7e3d3cbe558
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
    path: "EditorModel.Renumber"
    description:
      zh: >
          重排行号并逐行通知——一次性绑定不会自己重画。
          
      en: >
          Renumbers the rows and refreshes each one (one-time bindings do not repaint themselves).
          
  - protocol: rpc
    path: "StepVm.Edit"
    description:
      zh: >
          值没变就什么都不做的写入守卫。
          
      en: >
          The write guard that ignores a no-op value.
          
  - protocol: rpc
    path: "StepVm.BadgeBrush"
    description:
      zh: >
          类型徽标的面色：只有选中那一行是强调色。
          
      en: >
          The badge brush: the accent face only on the selected row.
          
deps:
  - kind: reference
    to: keymouse.flow.model
    to_api: "rpc:FlowDocument.Load"
    label: {zh: "同一份 schema", en: "The same schema"}
  - kind: reference
    to: keymouse.flow.model
    to_api: "rpc:FlowDocument.Save"
    label: {zh: "写回也是它", en: "And for writing back"}
  - kind: call
    to: keymouse.flow.schema
    from_api: "rpc:StepVm.BadgeBrush"
    to_api: "rpc:FlowSchema.FieldsFor"
    label: {zh: "字段由它给", en: "Fields come from it"}
---
