---
uid: 3c4d5e6f
id: keymouse.window
parent: keymouse
tags: [window, safety]
name: {zh: "窗口识别、闸门与聚焦", en: "Window identity, gate & focus"}
description:
  zh: >
      回答「这个窗口能不能安全地接收输入」：枚举顶层窗口 → 按选择器匹配 → 可见/最小化/遮盖/禁用/无响应逐项判定 → 聚焦并回读真实前台窗口确认。全部判定都是客观 API 查询，不含启发式猜测。
      
  en: >
      Answers whether a window may safely receive input: enumerate top-level windows, match the selector, judge visibility/minimized/cloaked/disabled/responding, then focus and read the foreground window back to confirm.
      
revision: e03d53e4c41a8a7220e83123d7f8a44a54ff8dab
updated_at: "2026-10-03T08:15:19.893Z"
fingerprint: 99039c06d973423bde9df1d9872d3f46516251ed96000c0019981083286fa8a2
source:
  - path: "WindowInfo.cs"
    line: 1
    end_line: 132
  - path: "WindowLocator.cs"
    line: 1
    end_line: 111
  - path: "WindowEligibility.cs"
    line: 1
    end_line: 73
  - path: "WindowFocus.cs"
    line: 1
    end_line: 58
  - path: "WindowResolver.cs"
    line: 1
    end_line: 45
deps:
  - kind: call
    to: keymouse.native
    label: {zh: "Win32 查询", en: "Win32 queries"}
  - kind: call
    to: keymouse.console
    label: {zh: "表格与状态串", en: "Tables & state text"}
  - kind: call
    to: keymouse.cli.failure
    label: {zh: "以退出码拒绝", en: "Refuse with a code"}
---
