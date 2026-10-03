---
uid: 0b1c2d3e
id: keymouse.cli.mouse.wheel
parent: keymouse.cli.mouse
tags: [mouse]
name: {zh: "滚轮命令", en: "Wheel commands"}
description:
  zh: >
      mouse wheel / hwheel：120 为一格，正负表示方向；与点击一样支持 -x/-y 先定位，且先移动再滚动以保证滚在目标窗口上。
      
  en: >
      mouse wheel / hwheel: 120 per notch, sign gives the direction, with an optional point to move to first so the wheel lands on the intended window.
      
revision: 01b8c3c91b12f7561f62aa9f6af36e7eb9e3bb94
updated_at: "2026-10-03T10:33:15.213Z"
fingerprint: bbcd909665ff033ce01c238b5437796aded55168ead822ef453ff228abd0fee5
source:
  - path: "Program.cs"
    line: 261
    end_line: 290
apis:
  - protocol: rpc
    path: "mouse wheel"
    description:
      zh: >
          纵向滚动（120 = 一格）。
          
      en: >
          Vertical scroll (120 = one notch).
          
  - protocol: rpc
    path: "mouse hwheel"
    description:
      zh: >
          横向滚动。
          
      en: >
          Horizontal scroll.
          
deps:
  - kind: call
    to: keymouse.input.wheel
    from_api: "rpc:mouse wheel"
    to_api: "rpc:NativeInput.Wheel"
    label: {zh: "滚轮事件", en: "Wheel event"}
---
