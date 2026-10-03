---
uid: de4f5a6b
id: keymouse.input.mouse-button
parent: keymouse.input
tags: [input, mouse]
name: {zh: "鼠标键事件", en: "Mouse button events"}
description:
  zh: >
      五种键（left/right/middle/x1/x2）的按下、抬起与连点，含连点间隔；X 键用 XBUTTON 数据字段区分。
      
  en: >
      Press, release and repeat-click for five buttons including the X buttons, which are told apart by the XBUTTON data field.
      
revision: 6347fbfbc3d7af8839f5dcc9c35501ce2484bdef
updated_at: "2026-10-03T11:11:56.202Z"
fingerprint: 313ba7f64e5fc1c76991cd00fadad0b4c74007b47eb4e68d1954227536a95c29
source:
  - path: "src/KeyMouse.Core/NativeInput.cs"
    line: 167
    end_line: 189
apis:
  - protocol: rpc
    path: "NativeInput.ButtonDown"
    description:
      zh: >
          按下鼠标键。
          
      en: >
          Presses a mouse button.
          
  - protocol: rpc
    path: "NativeInput.ButtonUp"
    description:
      zh: >
          松开鼠标键。
          
      en: >
          Releases a mouse button.
          
  - protocol: rpc
    path: "NativeInput.Click"
    description:
      zh: >
          点击/连点（含间隔）。
          
      en: >
          Clicks, repeated with an interval.
          
deps:
  - kind: call
    to: keymouse.input.send
    from_api: "rpc:NativeInput.Click"
    to_api: "rpc:NativeInput.Send"
    label: {zh: "投递", en: "Dispatch"}
  - kind: call
    to: keymouse.native.user32-input
    from_api: "rpc:NativeInput.Click"
    to_api: "rpc:user32!SendInput"
    label: {zh: "键位标志", en: "Button flags"}
---
