---
uid: 1e2f3a4b
id: keymouse.window.focus
parent: keymouse.window
tags: [window, focus]
name: {zh: "聚焦与前台回读", en: "Focus & foreground read-back"}
description:
  zh: >
      抢前台并回读验证：SetForegroundWindow 后重新问系统“现在谁是前台”，最多重试若干次；成功与否以真实前台窗口为准，而不是调用的返回值。
      
  en: >
      Takes the foreground and verifies it by asking the system who is actually foreground now, retrying a few times. Success is judged by the real foreground window, not by the API's return value.
      
revision: d870b0c5dfd7fe29675863c0288b572a6111a8d4
updated_at: "2026-10-03T07:28:04.467Z"
fingerprint: e80d813e3fde8d15422e352901107627efc0dcfcf007d3a2054e4497d244417a
source:
  - path: "WindowFocus.cs"
    line: 20
    end_line: 52
apis:
  - protocol: rpc
    path: "WindowFocus.Focus"
    description:
      zh: >
          聚焦并返回是否真的成功。
          
      en: >
          Focuses and reports whether it really worked.
          
  - protocol: rpc
    path: "WindowFocus.IsForeground"
    description:
      zh: >
          当前是否就是前台窗口。
          
      en: >
          Whether the window is foreground right now.
          
deps:
  - kind: call
    to: keymouse.native.user32-focus
    from_api: "rpc:WindowFocus.Focus"
    to_api: "rpc:user32!SetForegroundWindow"
    label: {zh: "抢前台与回读", en: "Set and read"}
---
