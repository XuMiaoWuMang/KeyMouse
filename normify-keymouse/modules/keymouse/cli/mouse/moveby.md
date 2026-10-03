---
uid: e9fa0b1c
id: keymouse.cli.mouse.moveby
parent: keymouse.cli.mouse
tags: [mouse]
name: {zh: "相对移动", en: "Relative move"}
description:
  zh: >
      mouse moveby <dx> <dy>：以当前位置为基准移动，内部回读当前位置后计算目标，避免相对移动在边界处静默丢失。
      
  en: >
      mouse moveby <dx> <dy> moves relative to the current position, reading it back first so a nudge near a screen edge does not silently vanish.
      
revision: d870b0c5dfd7fe29675863c0288b572a6111a8d4
updated_at: "2026-10-03T07:28:04.449Z"
fingerprint: 2002c7c0806c2cc60e9cdeb156af20af0b5bd3ede859541c1f12dbf68f0b71ac
source:
  - path: "Program.cs"
    line: 210
    end_line: 219
apis:
  - protocol: rpc
    path: "mouse moveby"
    description:
      zh: >
          相对当前位置移动。
          
      en: >
          Moves relative to the current position.
          
deps:
  - kind: call
    to: keymouse.input.mouse-move
    from_api: "rpc:mouse moveby"
    to_api: "rpc:NativeInput.MoveBy"
    label: {zh: "相对移动", en: "Relative move"}
---
