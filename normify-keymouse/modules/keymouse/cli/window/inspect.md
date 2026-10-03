---
uid: af1b2c3d
id: keymouse.cli.window.inspect
parent: keymouse.cli.window
tags: [window, diagnostics]
name: {zh: "窗口诊断", en: "Window inspect"}
description:
  zh: >
      对每个匹配窗口逐项给出可用性判定，并列出「问题」与「备注」两条清单——回答的是“它为什么不可用”，而不是“它不可用”。
      
  en: >
      For every matching window it prints the verdict plus separate problem and note lists, answering why a window is unusable rather than just that it is.
      
revision: d870b0c5dfd7fe29675863c0288b572a6111a8d4
updated_at: "2026-10-03T07:28:04.449Z"
fingerprint: 2002c7c0806c2cc60e9cdeb156af20af0b5bd3ede859541c1f12dbf68f0b71ac
source:
  - path: "Program.cs"
    line: 465
    end_line: 496
apis:
  - protocol: rpc
    path: "window inspect"
    description:
      zh: >
          解释窗口为何可用/不可用。
          
      en: >
          Explains why a window is usable or not.
          
deps:
  - kind: call
    to: keymouse.window
    label: {zh: "匹配与判定", en: "Match & judge"}
  - kind: call
    to: keymouse.console
    label: {zh: "状态串与表格", en: "State text & table"}
---
