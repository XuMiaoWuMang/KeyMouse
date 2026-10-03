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
      
revision: 8138caf9efa304f3487fcf5c6320ee4ac39f036b
updated_at: "2026-10-03T10:11:53.999Z"
fingerprint: 35dd1463817a882de031cd047ed74cc24435245ab8196fecfafdc46f8c895c78
source:
  - path: "Program.cs"
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
