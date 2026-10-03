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
      
revision: d870b0c5dfd7fe29675863c0288b572a6111a8d4
updated_at: "2026-10-03T07:28:04.469Z"
fingerprint: e52dac08492f5ae3f8efd6a9e9cbe6e5af9a49c1c1402c8c9bedaf366fa57b9c
source:
  - path: "WindowLocator.cs"
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
