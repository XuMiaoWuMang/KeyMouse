---
uid: 7a1f2002
id: keymouse.tests.unit.calls
parent: keymouse.tests.unit
tags: [test, unit]
name: {zh: "子流程单测", en: "Subflow tests"}
description:
  zh: >
      不需要桌面：`call` 被内联成一条计划、子流程自己的变量与调用者的变量同时可见、`vars` 覆盖子流程默认值且能引用调用者、子流程里的捕获不外泄、`export` 那一项确实把值搬回调用者那一帧；文件缺失、缺 `flow`、成环、嵌套两层的作用域都逐条钉住。
      
  en: >
      No desktop needed: a call is inlined into one plan, the subflow own variables and the caller values are both visible, vars win over the subflow defaults and may use the caller values, captures stay inside, and the export item really carries the value back into the caller frame; a missing file, a missing flow, a cycle and two levels of nesting are each pinned down.
      
revision: 1ebae14ff2430b597cc4a1695a71ddf788879db1
updated_at: "2026-10-03T12:49:24.516Z"
fingerprint: 90ea96f46a3c7c5d113455431800f51f9368921c24518b8a86739aee52bdff9e
source:
  - path: "tests/KeyMouse.Tests/CallTests.cs"
apis:
  - protocol: rpc
    path: "子流程断言组"
    description:
      zh: >
          内联、作用域、导出与拒绝。
          
      en: >
          Inlining, scope, export and refusal.
          
deps:
  - kind: reference
    to: keymouse.flow.plan
    to_api: "rpc:FlowPlan.Build"
    label: {zh: "被测的计划层", en: "Plan layer under test"}
  - kind: reference
    to: keymouse.flow.model
    to_api: "rpc:FlowDocument.Resolve"
    label: {zh: "按帧解析", en: "Resolve against a frame"}
---
