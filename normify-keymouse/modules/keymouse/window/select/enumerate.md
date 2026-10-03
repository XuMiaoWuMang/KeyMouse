---
uid: a7b8c9da
id: keymouse.window.select.enumerate
parent: keymouse.window.select
tags: [window]
name: {zh: "顶层窗口枚举", en: "Top-level enumeration"}
description:
  zh: >
      用 EnumWindows 拿全部顶层窗口，跳过句柄无效的项；也可选地对每个窗口做一次 WM_NULL 探测（只有 window list 与最终候选需要）。
      
  en: >
      Walks every top-level window with EnumWindows, skipping invalid handles, optionally probing each one with WM_NULL (only `window list` and the final candidates need that).
      
revision: 6347fbfbc3d7af8839f5dcc9c35501ce2484bdef
updated_at: "2026-10-03T11:11:56.217Z"
fingerprint: dd1baf24ba788d3671c7613d3c370b5b1cf8f263253cd39ec29217d797b6df20
source:
  - path: "src/KeyMouse.Core/WindowLocator.cs"
    line: 41
    end_line: 51
apis:
  - protocol: rpc
    path: "WindowLocator.EnumerateTopLevel"
    description:
      zh: >
          枚举全部顶层窗口快照。
          
      en: >
          Enumerates snapshots of every top-level window.
          
deps:
  - kind: call
    to: keymouse.native.user32-enumerate
    from_api: "rpc:WindowLocator.EnumerateTopLevel"
    to_api: "rpc:user32!EnumWindows"
    label: {zh: "EnumWindows", en: "EnumWindows"}
  - kind: call
    to: keymouse.window.snapshot
    from_api: "rpc:WindowLocator.EnumerateTopLevel"
    to_api: "rpc:WindowInfo.Capture"
    label: {zh: "逐个拍快照", en: "Snapshot each"}
---
