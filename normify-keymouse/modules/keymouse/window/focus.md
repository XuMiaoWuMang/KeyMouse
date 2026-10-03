---
uid: 1e2f3a4b
id: keymouse.window.focus
parent: keymouse.window
tags: [window, focus]
name: {zh: "聚焦与前台回读", en: "Focus & foreground read-back"}
description:
  zh: >
      聚焦**验证过**而不是假设过：调完 SetForegroundWindow 会回读真实前台窗口，只有真的变成目标才返回成功；失败时点名当前占着前台的是谁。调用前先 AttachThreadInput 把当前前台线程的输入队列接上，让这次调用合法——不注入任何按键（实测：不接输入队列时三次温和尝试全败，退出码 5，前台是别的进程；接上后同一条命令退出码 0）。
      
  en: >
      Focus that is verified rather than assumed: after SetForegroundWindow it reads the real foreground window back and only succeeds if the target got it; a failure names whoever holds the foreground. Before the call it attaches our input queue to the foreground thread, which makes the call legal and injects no keys (measured: without attaching, three gentle attempts failed with exit 5 while another process held the foreground; with it, the same command exits 0).
      
revision: 96c306061d63c034a01c2369e4abc295251e2f35
updated_at: "2026-10-03T13:37:48.970Z"
fingerprint: 5387a81602659f248bfb731a7d430eb132d7b649b7f24d42246b32f2fe826482
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
