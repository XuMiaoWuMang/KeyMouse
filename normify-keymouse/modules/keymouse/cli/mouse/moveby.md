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
      
revision: 6347fbfbc3d7af8839f5dcc9c35501ce2484bdef
updated_at: "2026-10-03T11:11:56.197Z"
fingerprint: 7f2ba5c7c2569f29daf25ca2416e5988b42986f24461e2467265c7fae3dc41e9
source:
  - path: "src/KeyMouse.Core/Commands.cs"
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
