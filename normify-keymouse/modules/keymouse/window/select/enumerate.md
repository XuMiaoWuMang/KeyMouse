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
      
revision: d870b0c5dfd7fe29675863c0288b572a6111a8d4
updated_at: "2026-10-03T07:28:04.468Z"
fingerprint: e52dac08492f5ae3f8efd6a9e9cbe6e5af9a49c1c1402c8c9bedaf366fa57b9c
source:
  - path: "WindowLocator.cs"
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
