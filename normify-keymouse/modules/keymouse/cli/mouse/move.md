---
uid: d8e9fa0b
id: keymouse.cli.mouse.move
parent: keymouse.cli.mouse
tags: [mouse]
name: {zh: "移动（绝对/客户区）", en: "Move (absolute / client)"}
description:
  zh: >
      两种移动：mouse move <x> <y> 直接给屏幕像素；带 -wx/-wy 时先选目标窗口、把客户区坐标换算成屏幕坐标再移（需要选择器，否则报错）。
      
  en: >
      Two forms: absolute screen pixels, or client-area coordinates that are mapped through the target window first (which requires a selector).
      
revision: 8138caf9efa304f3487fcf5c6320ee4ac39f036b
updated_at: "2026-10-03T10:11:54.000Z"
fingerprint: 35dd1463817a882de031cd047ed74cc24435245ab8196fecfafdc46f8c895c78
source:
  - path: "Program.cs"
    line: 189
    end_line: 209
apis:
  - protocol: rpc
    path: "mouse move"
    description:
      zh: >
          移到绝对屏幕坐标。
          
      en: >
          Moves to absolute screen coordinates.
          
  - protocol: rpc
    path: "mouse move -wx -wy"
    description:
      zh: >
          移到目标窗口客户区的点。
          
      en: >
          Moves to a client-area point of the target.
          
deps:
  - kind: call
    to: keymouse.input.mouse-move
    from_api: "rpc:mouse move"
    to_api: "rpc:NativeInput.MoveTo"
    label: {zh: "移动光标", en: "Move cursor"}
  - kind: call
    to: keymouse.window
    label: {zh: "客户区→屏幕", en: "Client to screen"}
---
