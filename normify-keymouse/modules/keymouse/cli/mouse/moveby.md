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
      
revision: 8138caf9efa304f3487fcf5c6320ee4ac39f036b
updated_at: "2026-10-03T10:11:54.000Z"
fingerprint: 35dd1463817a882de031cd047ed74cc24435245ab8196fecfafdc46f8c895c78
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
