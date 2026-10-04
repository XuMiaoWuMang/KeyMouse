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
      
revision: ef3cf740e80bd62dd7540df6ee4c526380163233
updated_at: "2026-10-04T14:22:17.412Z"
fingerprint: c2466d777f9e13ef4b2681d5d98fa1383d4339672611d90ef8fad5764cbbc7fc
source:
  - path: "src/KeyMouse.Core/Commands.cs"
    line: 431
    end_line: 545
deps:
  - kind: call
    to: keymouse.window
    label: {zh: "枚举、匹配、判定", en: "Enumerate, match, judge"}
---
