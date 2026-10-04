---
uid: 7a1f1007
id: keymouse.editor.inspector
parent: keymouse.editor
tags: [editor, ui, contract]
name: {zh: "契约驱动的参数界面", en: "Contract-driven inspector"}
description:
  zh: >
      参数面板完全由流程契约渲染：契约里的 kind 决定用哪种控件（text/multiline/int/bool/enum/point/region/target/when/steps/vars/stringList），必填、条件必填、范围与枚举中文标签也全来自同一份文件。默认值写进输入框里，解释收进一个小信息标记，校验错误写在出错字段的下方——不用一个会消失的弹窗打发人。
      
  en: >
      The parameter panel, rendered from the flow contract: the contract's kind decides the control (text, multiline, int, bool, enum, point, region, target, when, steps, vars, stringList), and required, conditionally-required, ranges and Chinese enum labels all come from the same file. Defaults live in the field itself, explanations sit behind a small info mark, and a validation error is written under the field that caused it instead of in a popup that disappears.
      
revision: ef3cf740e80bd62dd7540df6ee4c526380163233
updated_at: "2026-10-04T14:24:35.926Z"
fingerprint: a15e0bb4af2787607795ad0cbf94add7566d80f37a6ea47b32b541afb55f0dbb
source:
  - path: "editor/KeyMouse.FlowEditor/InspectorBuilder.cs"
  - path: "editor/KeyMouse.FlowEditor/InspectorFields.cs"
apis:
  - protocol: rpc
    path: "InspectorBuilder.Build"
    description:
      zh: >
          为选中的一步搭出整张参数卡。
          
      en: >
          Builds the whole parameter card for the selected step.
          
  - protocol: rpc
    path: "InspectorFields.ValueLabel"
    description:
      zh: >
          枚举值的中文标签：机器值不变，人看到中文。
          
      en: >
          Value labels: an enum's machine value vs the Chinese a human reads.
          
deps:
  - kind: call
    to: keymouse.flow.schema
    to_api: "rpc:FlowSchema.FieldsFor"
    label: {zh: "问契约要字段", en: "Asks the contract"}
  - kind: call
    to: keymouse.editor.steps
    to_api: "rpc:StepVm.Edit"
    label: {zh: "写回选中的一步", en: "Writes into the step"}
---
