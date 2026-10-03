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
      
revision: 6347fbfbc3d7af8839f5dcc9c35501ce2484bdef
updated_at: "2026-10-03T11:11:56.216Z"
fingerprint: 3c23d3ba0bfa991fcdd14654409713ca0a20afb302f4aafacd056057428e7af4
source:
  - path: "src/KeyMouse.Core/WindowFocus.cs"
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
