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
      
revision: 6347fbfbc3d7af8839f5dcc9c35501ce2484bdef
updated_at: "2026-10-03T11:11:56.197Z"
fingerprint: 7f2ba5c7c2569f29daf25ca2416e5988b42986f24461e2467265c7fae3dc41e9
source:
  - path: "src/KeyMouse.Core/Commands.cs"
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
