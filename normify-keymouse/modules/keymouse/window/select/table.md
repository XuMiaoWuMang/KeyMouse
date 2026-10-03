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
      
revision: 6347fbfbc3d7af8839f5dcc9c35501ce2484bdef
updated_at: "2026-10-03T11:11:56.218Z"
fingerprint: dd1baf24ba788d3671c7613d3c370b5b1cf8f263253cd39ec29217d797b6df20
source:
  - path: "src/KeyMouse.Core/WindowLocator.cs"
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
