---
uid: 7a1f100c
id: keymouse.docs.interface
parent: keymouse.docs
tags: [docs, contract]
name: {zh: "接口文档", en: "Interface document"}
description:
  zh: >
      JSON 参数契约：每个入口里哪些字段必须、哪些可选，以及 WinUI 编辑器该怎么用。规矩只有一条——前端只依据契约渲染，加载器只依据契约校验，两边都不许自己另写一套"哪种步骤有哪些字段"（当初正是这么脱节的：`click-text` 在界面上只剩目标与备注）。另附一张常驻 Runner 的 IPC 方法表摘要，规范全文指向 runner-protocol.md。
      
  en: >
      The JSON parameter contract: which fields are required and which are optional for every entry point, and how the WinUI editor uses them. One rule: the front end renders from the contract and the loader validates from it - neither writes a private table of 'which step has which fields' (that is how click-text once lost its region, text, match and timeout in the UI). It also carries a short table of the Runner's IPC methods, with runner-protocol.md as the norm.
      
revision: ef3cf740e80bd62dd7540df6ee4c526380163233
updated_at: "2026-10-04T14:24:35.928Z"
fingerprint: bc3d8185229644b2135c23a7b13457b509c94f9247517ae01c2c71b3c340ba5b
source:
  - path: "docs/interface.md"
apis:
  - protocol: file
    path: "docs/interface.md"
    description:
      zh: >
          每个入口里哪些 JSON 字段必须、哪些可选。
          
      en: >
          Which JSON fields are required and which are optional, for every entry point.
          
deps:
  - kind: reference
    to: keymouse.flow.schema
    to_api: "rpc:FlowSchema.Steps"
    label: {zh: "规矩来自契约", en: "Rules from the contract"}
  - kind: reference
    to: keymouse.runner.protocol.methods
    to_api: "rpc:keymouse-runner 请求"
    label: {zh: "IPC 方法表摘要", en: "IPC summary"}
---
