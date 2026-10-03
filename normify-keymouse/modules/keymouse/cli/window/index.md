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
      
revision: e03d53e4c41a8a7220e83123d7f8a44a54ff8dab
updated_at: "2026-10-03T08:15:19.853Z"
fingerprint: 1177fec2587430ef3152436f51e478c00e5e3607fc21abd0f28040dc25fcc1ef
source:
  - path: "Program.cs"
    line: 431
    end_line: 545
deps:
  - kind: call
    to: keymouse.window
    label: {zh: "枚举、匹配、判定", en: "Enumerate, match, judge"}
---
