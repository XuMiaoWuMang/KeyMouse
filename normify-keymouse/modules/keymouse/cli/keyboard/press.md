---
uid: 4f5a6b7c
id: keymouse.cli.keyboard.press
parent: keymouse.cli.keyboard
tags: [keyboard]
name: {zh: "敲击（含连按）", en: "Press (with repeat)"}
description:
  zh: >
      key press <按键> [-n 次数] [-i 间隔]：按下再抬起，可连按；每次敲击都落在同一个已验证的目标窗口上。
      
  en: >
      key press <key> [-n count] [-i interval]: taps a key, optionally repeated, always against the one verified target window.
      
revision: ef3cf740e80bd62dd7540df6ee4c526380163233
updated_at: "2026-10-04T14:22:17.409Z"
fingerprint: c2466d777f9e13ef4b2681d5d98fa1383d4339672611d90ef8fad5764cbbc7fc
source:
  - path: "src/KeyMouse.Core/Commands.cs"
    line: 363
    end_line: 379
apis:
  - protocol: rpc
    path: "key press"
    description:
      zh: >
          敲击按键（可连按）。
          
      en: >
          Taps a key, optionally repeated.
          
deps:
  - kind: call
    to: keymouse.input.key
    from_api: "rpc:key press"
    to_api: "rpc:NativeInput.Tap"
    label: {zh: "敲击", en: "Tap"}
---
