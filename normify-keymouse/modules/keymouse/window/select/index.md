---
uid: e5f6a7b8
id: keymouse.window.select
parent: keymouse.window
tags: [window]
name: {zh: "选择与匹配", en: "Selection & matching"}
description:
  zh: >
      把「--title/--class/--process/--pid/--hwnd」翻译成一组窗口：枚举 → 逐条件 AND 匹配 → 必要时回显候选表或提示。
      
  en: >
      Turns a selector into a set of windows: enumerate, match every condition with AND, and show candidate tables or hints when nothing matches.
      
revision: 6347fbfbc3d7af8839f5dcc9c35501ce2484bdef
updated_at: "2026-10-03T11:11:56.218Z"
fingerprint: dd1baf24ba788d3671c7613d3c370b5b1cf8f263253cd39ec29217d797b6df20
source:
  - path: "src/KeyMouse.Core/WindowLocator.cs"
    line: 6
    end_line: 111
deps:
  - kind: call
    to: keymouse.window.snapshot
    label: {zh: "读取窗口属性", en: "Read window facts"}
  - kind: call
    to: keymouse.console
    label: {zh: "表格与提示", en: "Table and hints"}
---
