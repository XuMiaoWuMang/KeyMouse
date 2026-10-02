---
uid: cd3e4f5a
id: keymouse.input.mouse-move
parent: keymouse.input
tags: [input, mouse]
name: {zh: "光标移动", en: "Cursor movement"}
description:
  zh: >
      绝对移动用 MOUSEEVENTF_ABSOLUTE|VIRTUALDESK 归一化到整个虚拟桌面（多屏也准），并保留 SetCursorPos 回退；相对移动直接投递 MOVE。
      
  en: >
      Absolute movement normalises against the whole virtual desktop so multi-monitor setups stay accurate, with a SetCursorPos fallback; relative movement posts a plain MOVE.
      
revision: 8d69164cfe5fa0896f595c0bca51f97977ff51b9
updated_at: "2026-10-02T17:37:21.449Z"
fingerprint: 3618318b51206f21111b1abedb3cc5ef20caa74b67c90f6583a8fea20b8a2783
source:
  - path: "NativeInput.cs"
    line: 133
    end_line: 165
apis:
  - protocol: rpc
    path: "NativeInput.MoveTo"
    description:
      zh: >
          移到绝对屏幕坐标（含回退）。
          
      en: >
          Moves to absolute screen coordinates.
          
  - protocol: rpc
    path: "NativeInput.MoveBy"
    description:
      zh: >
          相对移动。
          
      en: >
          Moves by a delta.
          
deps:
  - kind: call
    to: keymouse.input.send
    from_api: "rpc:NativeInput.MoveTo"
    to_api: "rpc:NativeInput.Send"
    label: {zh: "投递", en: "Dispatch"}
  - kind: call
    to: keymouse.native.user32-input
    from_api: "rpc:NativeInput.MoveTo"
    to_api: "rpc:user32!GetSystemMetrics"
    label: {zh: "坐标空间", en: "Coordinate space"}
---
