---
uid: ef5a6b7c
id: keymouse.input.wheel
parent: keymouse.input
tags: [input, mouse]
name: {zh: "滚轮事件", en: "Wheel events"}
description:
  zh: >
      纵向与横向滚轮，增量为 120 的整数倍，方向由符号决定。
      
  en: >
      Vertical and horizontal wheel events in multiples of 120, direction given by the sign.
      
revision: 6347fbfbc3d7af8839f5dcc9c35501ce2484bdef
updated_at: "2026-10-03T11:11:56.202Z"
fingerprint: 313ba7f64e5fc1c76991cd00fadad0b4c74007b47eb4e68d1954227536a95c29
source:
  - path: "src/KeyMouse.Core/NativeInput.cs"
    line: 190
    end_line: 192
apis:
  - protocol: rpc
    path: "NativeInput.Wheel"
    description:
      zh: >
          滚轮（横向可选）。
          
      en: >
          Wheel scroll, horizontal optional.
          
deps:
  - kind: call
    to: keymouse.input.send
    from_api: "rpc:NativeInput.Wheel"
    to_api: "rpc:NativeInput.Send"
    label: {zh: "投递", en: "Dispatch"}
---
