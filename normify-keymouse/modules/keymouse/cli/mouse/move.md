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
      
revision: 6347fbfbc3d7af8839f5dcc9c35501ce2484bdef
updated_at: "2026-10-03T11:11:56.197Z"
fingerprint: 7f2ba5c7c2569f29daf25ca2416e5988b42986f24461e2467265c7fae3dc41e9
source:
  - path: "src/KeyMouse.Core/Commands.cs"
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
