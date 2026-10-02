---
uid: fc0d1e2f
id: keymouse.window.select.table
parent: keymouse.window.select
tags: [window, rendering]
name: {zh: "候选表格", en: "Candidate table"}
description:
  zh: >
      把一组窗口渲染成带表头的多行文本，供 window list 与错误提示共用。
      
  en: >
      Renders a set of windows as a headed multi-row table, shared by `window list` and the error hints.
      
revision: 8d69164cfe5fa0896f595c0bca51f97977ff51b9
updated_at: "2026-10-02T17:37:21.450Z"
fingerprint: e52dac08492f5ae3f8efd6a9e9cbe6e5af9a49c1c1402c8c9bedaf366fa57b9c
source:
  - path: "WindowLocator.cs"
    line: 109
    end_line: 111
apis:
  - protocol: rpc
    path: "WindowLocator.CandidateTable"
    description:
      zh: >
          多窗口表格文本。
          
      en: >
          Multi-window table text.
          
deps:
  - kind: call
    to: keymouse.console.width
    from_api: "rpc:WindowLocator.CandidateTable"
    to_api: "rpc:ConsoleText.Pad"
    label: {zh: "宽度对齐", en: "Width alignment"}
---
