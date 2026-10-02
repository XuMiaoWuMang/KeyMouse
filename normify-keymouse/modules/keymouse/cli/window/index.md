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
revision: 8d69164cfe5fa0896f595c0bca51f97977ff51b9
updated_at: "2026-10-02T17:55:00Z"
fingerprint: ed066abe077cf617475b89420866941ec0b7c26b595508435b3db3daac6799d5
source:
  - path: "Program.cs"
    line: 431
    end_line: 545
deps:
  - kind: call
    to: keymouse.window
    label: {zh: "枚举、匹配、判定", en: "Enumerate, match, judge"}
---
