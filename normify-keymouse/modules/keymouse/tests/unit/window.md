---
uid: 6c1e7c8d
id: keymouse.tests.unit.window
parent: keymouse.tests.unit
tags: [test]
name: {zh: "窗口用例", en: "Window cases"}
description:
  zh: >
      选择器匹配、候选收敛、状态串与闸门判定这些纯函数的断言；状态串用中文 token（可见/隐藏/未探测/无响应/已禁用），测的是“未探测”与“无响应”不被混为一谈。
      
  en: >
      Assertions for the pure window functions: selector matching, candidate preference, state text and gate verdicts, including that not-probed and no-answer stay distinct.
      
revision: d870b0c5dfd7fe29675863c0288b572a6111a8d4
updated_at: "2026-10-03T07:28:04.467Z"
fingerprint: 5e9aafbe1597a6b6d8442db5bb7d64049bf6e2f2e780ccd8b7e8805d20c3a6e0
source:
  - path: "tests/KeyMouse.Tests/WindowTests.cs"
    line: 1
    end_line: 122
apis:
  - protocol: rpc
    path: "WindowTests.Run"
    description:
      zh: >
          跑完所有窗口纯函数用例。
          
      en: >
          Runs every window-function case.
          
deps:
  - kind: reference
    to: keymouse.window.snapshot
    from_api: "rpc:WindowTests.Run"
    to_api: "rpc:WindowInfo.StateSummary"
    label: {zh: "状态串", en: "State text"}
---
