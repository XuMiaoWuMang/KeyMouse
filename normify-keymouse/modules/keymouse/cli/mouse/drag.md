---
uid: 1c2d3e4f
id: keymouse.cli.mouse.drag
parent: keymouse.cli.mouse
tags: [mouse]
name: {zh: "拖拽命令", en: "Drag command"}
description:
  zh: >
      两种拖拽：四个绝对坐标，或 -wx/-wy 起点 + --wx2/--wy2 终点的窗口相对形式（四个坐标必须一并给出，两个形式不能混用）。可指定按键、步数与总时长。
      
  en: >
      Two forms: four absolute coordinates, or client-relative start/end (all four required, never mixed with absolute). Button, steps and duration are optional.
      
revision: e03d53e4c41a8a7220e83123d7f8a44a54ff8dab
updated_at: "2026-10-03T08:15:19.850Z"
fingerprint: 1177fec2587430ef3152436f51e478c00e5e3607fc21abd0f28040dc25fcc1ef
source:
  - path: "Program.cs"
    line: 291
    end_line: 341
apis:
  - protocol: rpc
    path: "mouse drag"
    description:
      zh: >
          绝对坐标拖拽。
          
      en: >
          Drag between absolute points.
          
  - protocol: rpc
    path: "mouse drag -wx -wy --wx2 --wy2"
    description:
      zh: >
          窗口客户区内拖拽（如选字）。
          
      en: >
          Drag inside a window's client area.
          
deps:
  - kind: call
    to: keymouse.input.drag
    from_api: "rpc:mouse drag"
    to_api: "rpc:NativeInput.Drag"
    label: {zh: "拖拽序列", en: "Drag sequence"}
  - kind: call
    to: keymouse.window
    label: {zh: "两端坐标换算", en: "Map both ends"}
---
