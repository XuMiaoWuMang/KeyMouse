---
uid: 8d9e0f1a
id: keymouse.cli.window
parent: keymouse.cli
tags: [window]
name: {zh: "窗口子命令组", en: "Window command group"}
description:
  zh: >
      window 下的三个子命令：列出、诊断、聚焦。它是排查“为什么这条命令不肯发”的第一入口，也是脚本里设定当前目标的方式。
      
  en: >
      Three subcommands: list, inspect, focus. The first thing to reach for when a command refuses to send, and how a script sets its current target.
      
revision: 6347fbfbc3d7af8839f5dcc9c35501ce2484bdef
updated_at: "2026-10-03T11:11:56.198Z"
fingerprint: 7f2ba5c7c2569f29daf25ca2416e5988b42986f24461e2467265c7fae3dc41e9
source:
  - path: "src/KeyMouse.Core/Commands.cs"
    line: 431
    end_line: 545
deps:
  - kind: call
    to: keymouse.window
    label: {zh: "枚举、匹配、判定", en: "Enumerate, match, judge"}
---
