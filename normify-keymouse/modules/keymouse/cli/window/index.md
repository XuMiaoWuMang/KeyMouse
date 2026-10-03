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
      
revision: 8138caf9efa304f3487fcf5c6320ee4ac39f036b
updated_at: "2026-10-03T10:11:54.001Z"
fingerprint: 35dd1463817a882de031cd047ed74cc24435245ab8196fecfafdc46f8c895c78
source:
  - path: "Program.cs"
    line: 431
    end_line: 545
deps:
  - kind: call
    to: keymouse.window
    label: {zh: "枚举、匹配、判定", en: "Enumerate, match, judge"}
---
