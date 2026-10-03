---
uid: d4e5f6a7
id: keymouse.window.snapshot
parent: keymouse.window
tags: [window, perf]
name: {zh: "窗口快照", en: "Window snapshot"}
description:
  zh: >
      每个窗口一份惰性快照：标题/类名/进程号一开始就读，可见性/最小化/遮盖/属主/矩形/进程名则首次读取时才从句柄现取并缓存。一条带选择器的命令要枚举近 400 个窗口，这个惰性设计把每窗 12 次 API 降到 4 次（实测 439ms → 3ms）。
      
  en: >
      One lazy snapshot per window: title, class and pid are read up front; visibility, minimized, cloaked, owner, rect and process name are resolved from the handle on first read and cached. Enumerating ~400 windows per targeted command drops from a dozen API calls each to four.
      
revision: 6347fbfbc3d7af8839f5dcc9c35501ce2484bdef
updated_at: "2026-10-03T11:11:56.218Z"
fingerprint: 30e50255e2bd01aa5d964e946c1ae8509144bfce5a21e9144c6c7dcb17f108c4
source:
  - path: "src/KeyMouse.Core/WindowInfo.cs"
    line: 10
    end_line: 131
apis:
  - protocol: rpc
    path: "WindowInfo.Capture"
    description:
      zh: >
          取一份窗口快照（可选 WM_NULL 探测）。
          
      en: >
          Takes a window snapshot, optionally probing responsiveness.
          
  - protocol: rpc
    path: "WindowInfo.StateSummary"
    description:
      zh: >
          状态串：可见/最小化/遮盖/属主/禁用/响应。
          
      en: >
          State text: visible, minimized, cloaked, owner, disabled, responding.
          
  - protocol: rpc
    path: "WindowInfo.TableRow"
    description:
      zh: >
          window list 的一行（按显示宽度对齐）。
          
      en: >
          One window-list row, aligned by display width.
          
  - protocol: rpc
    path: "WindowInfo.Describe"
    description:
      zh: >
          单行描述（句柄/进程/标题/类名/状态）。
          
      en: >
          One-line description of a window.
          
deps:
  - kind: call
    to: keymouse.native.user32-enumerate
    from_api: "rpc:WindowInfo.Capture"
    to_api: "rpc:user32!GetWindowText"
    label: {zh: "标题与类名", en: "Title and class"}
  - kind: call
    to: keymouse.native.user32-state
    from_api: "rpc:WindowInfo.StateSummary"
    to_api: "rpc:user32!IsIconic"
    label: {zh: "可见与最小化", en: "Visible, iconic"}
  - kind: call
    to: keymouse.native.dwm
    from_api: "rpc:WindowInfo.StateSummary"
    to_api: "rpc:NativeWindow.IsCloaked"
    label: {zh: "DWM 遮盖", en: "DWM cloaking"}
  - kind: call
    to: keymouse.native.process
    from_api: "rpc:WindowInfo.Capture"
    to_api: "rpc:NativeWindow.ProcessNameOf"
    label: {zh: "进程名", en: "Process name"}
---
