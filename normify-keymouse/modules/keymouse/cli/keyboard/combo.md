---
uid: 6b7c8d9e
id: keymouse.cli.keyboard.combo
parent: keymouse.cli.keyboard
tags: [keyboard]
name: {zh: "组合键", en: "Key chord"}
description:
  zh: >
      key combo ctrl+shift+s [--hold 毫秒]：按顺序按下（修饰键在前）、按相反顺序抬起，中间保持 hold 毫秒，避免应用把快速组合识别成单键。
      
  en: >
      key combo ctrl+shift+s [--hold ms]: presses in order, releases in reverse, holding for the given time so the application does not mistake it for separate keys.
      
revision: 6347fbfbc3d7af8839f5dcc9c35501ce2484bdef
updated_at: "2026-10-03T11:11:56.195Z"
fingerprint: 7f2ba5c7c2569f29daf25ca2416e5988b42986f24461e2467265c7fae3dc41e9
source:
  - path: "src/KeyMouse.Core/Commands.cs"
    line: 400
    end_line: 413
apis:
  - protocol: rpc
    path: "key combo"
    description:
      zh: >
          发送组合键（如 ctrl+shift+s）。
          
      en: >
          Sends a chord such as ctrl+shift+s.
          
deps:
  - kind: call
    to: keymouse.input.key
    from_api: "rpc:key combo"
    to_api: "rpc:NativeInput.Chord"
    label: {zh: "组合键", en: "Chord"}
---
