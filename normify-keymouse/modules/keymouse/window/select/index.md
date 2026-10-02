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
revision: 8d69164cfe5fa0896f595c0bca51f97977ff51b9
updated_at: "2026-10-02T18:00:00Z"
fingerprint: e52dac08492f5ae3f8efd6a9e9cbe6e5af9a49c1c1402c8c9bedaf366fa57b9c
source:
  - path: "WindowLocator.cs"
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
