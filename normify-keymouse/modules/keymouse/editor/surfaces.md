---
uid: 7a1f1009
id: keymouse.editor.surfaces
parent: keymouse.editor
tags: [editor, ui]
name: {zh: "空状态与步骤选择面", en: "Empty state and step picker"}
description:
  zh: >
      两处都是**回答用户的问题**，而不是把组件摆开：空状态回答"从哪儿开始"（最近打开的那个文件通常就是答案），添加步骤的面回答"这一步要哪个类型"（按用途分组，每组一句话说明它干什么）。文案来自契约里每种步骤自己的摘要，不在这里另编一份。
      
  en: >
      The two places that are organised as answers rather than as component dumps: the empty state answers 'where do I start' (the most recent file usually is the answer) and the add-step surface answers 'which type do I need' (grouped by purpose, each group with one line saying what it does). Wording comes from the contract - each step type's own summary - so nothing is written twice here.
      
revision: ef3cf740e80bd62dd7540df6ee4c526380163233
updated_at: "2026-10-04T14:24:35.927Z"
fingerprint: c13989c35bbe86378e44476049e6093f37ab311ef63a32a4a1596340bef9ae7b
source:
  - path: "editor/KeyMouse.FlowEditor/Surfaces.cs"
apis:
  - protocol: rpc
    path: "Surfaces.FillRecent"
    description:
      zh: >
          填起始面的最近打开列表，并决定那一块显不显示。
          
      en: >
          Fills the start page's recent list and shows or hides that block.
          
  - protocol: rpc
    path: "Surfaces.Groups"
    description:
      zh: >
          步骤按用途分组：窗口 / 输入 / 看屏幕 / 流程。
          
      en: >
          The step types grouped by purpose: window, input, screen, flow.
          
deps:
  - kind: reference
    to: keymouse.flow.schema
    to_api: "rpc:FlowSchema.TryGetStep"
    label: {zh: "摘要文案来自契约", en: "Summaries come from it"}
---
