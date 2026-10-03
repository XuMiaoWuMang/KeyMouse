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
      
revision: d870b0c5dfd7fe29675863c0288b572a6111a8d4
updated_at: "2026-10-03T07:28:04.447Z"
fingerprint: 2002c7c0806c2cc60e9cdeb156af20af0b5bd3ede859541c1f12dbf68f0b71ac
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
