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
      
revision: 6347fbfbc3d7af8839f5dcc9c35501ce2484bdef
updated_at: "2026-10-03T11:11:56.202Z"
fingerprint: 313ba7f64e5fc1c76991cd00fadad0b4c74007b47eb4e68d1954227536a95c29
source:
  - path: "src/KeyMouse.Core/NativeInput.cs"
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
