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
      
revision: 8d69164cfe5fa0896f595c0bca51f97977ff51b9
updated_at: "2026-10-02T17:37:21.449Z"
fingerprint: ed066abe077cf617475b89420866941ec0b7c26b595508435b3db3daac6799d5
source:
  - path: "Program.cs"
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
