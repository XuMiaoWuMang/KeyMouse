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
      
revision: e03d53e4c41a8a7220e83123d7f8a44a54ff8dab
updated_at: "2026-10-03T08:15:19.858Z"
fingerprint: 3618318b51206f21111b1abedb3cc5ef20caa74b67c90f6583a8fea20b8a2783
source:
  - path: "NativeInput.cs"
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
