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
      
revision: d870b0c5dfd7fe29675863c0288b572a6111a8d4
updated_at: "2026-10-03T07:28:04.453Z"
fingerprint: 3618318b51206f21111b1abedb3cc5ef20caa74b67c90f6583a8fea20b8a2783
source:
  - path: "NativeInput.cs"
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
