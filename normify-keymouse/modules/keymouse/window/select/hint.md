---
uid: ebfc0d1e
id: keymouse.window.select.hint
parent: keymouse.window.select
tags: [window, diagnostics]
name: {zh: "无匹配提示", en: "No-match hint"}
description:
  zh: >
      选择器没匹配到窗口时，附上当前可见窗口样例让用户对照——复用已枚举的列表，不重新扫一遍桌面。
      
  en: >
      When a selector matches nothing, appends a sample of the visible windows for the user to compare against, reusing the enumeration already in hand.
      
revision: 6347fbfbc3d7af8839f5dcc9c35501ce2484bdef
updated_at: "2026-10-03T11:11:56.218Z"
fingerprint: dd1baf24ba788d3671c7613d3c370b5b1cf8f263253cd39ec29217d797b6df20
source:
  - path: "src/KeyMouse.Core/WindowLocator.cs"
    line: 97
    end_line: 108
apis:
  - protocol: rpc
    path: "WindowLocator.NoMatchMessage"
    description:
      zh: >
          生成“没有匹配”的错误文案。
          
      en: >
          Builds the no-match error message.
          
deps:
  - kind: call
    to: keymouse.window.select.table
    from_api: "rpc:WindowLocator.NoMatchMessage"
    to_api: "rpc:WindowLocator.CandidateTable"
    label: {zh: "复用候选表", en: "Reuse the table"}
---
