---
uid: b6c7d8e9
id: keymouse.cli.mouse
parent: keymouse.cli
tags: [mouse]
name: {zh: "鼠标子命令组", en: "Mouse command group"}
description:
  zh: >
      mouse 下的全部子命令：坐标查询、移动（绝对/相对/客户区）、点击与按住、滚轮、拖拽。每个子命令都是「先验证目标、再注入」的完整一步。
      
  en: >
      Every mouse subcommand: cursor query, movement (absolute, relative, client-relative), clicks and holds, wheels, and drag. Each one is a complete verify-then-inject step.
      
revision: e03d53e4c41a8a7220e83123d7f8a44a54ff8dab
updated_at: "2026-10-03T08:15:19.851Z"
fingerprint: 1177fec2587430ef3152436f51e478c00e5e3607fc21abd0f28040dc25fcc1ef
source:
  - path: "Program.cs"
    line: 169
    end_line: 341
deps:
  - kind: call
    to: keymouse.input
    label: {zh: "注入鼠标事件", en: "Inject mouse events"}
  - kind: call
    to: keymouse.cli.window.targeting
    label: {zh: "选目标并聚焦", en: "Pick and focus target"}
---
