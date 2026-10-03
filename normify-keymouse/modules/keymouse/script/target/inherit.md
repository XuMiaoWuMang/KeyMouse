---
uid: ae5f6a7b
id: keymouse.script.target.inherit
parent: keymouse.script.target
tags: [script, target]
name: {zh: "继承判定", en: "Inheritance rule"}
description:
  zh: >
      只有 mouse / key 会继承；waitfor、waitgone、window list 刻意不继承——等哪个窗口、列哪些窗口是应该明说的意图。
      
  en: >
      Only mouse and key inherit. waitfor, waitgone and window list deliberately do not: which window you wait for, or list, is worth stating explicitly.
      
revision: 1b316650150f1540368ee548f7d9992ccaa3239e
updated_at: "2026-10-03T11:27:16.008Z"
fingerprint: 77e8c215319806cea2fd833e231759721bc3125357c377795fe231100c8ddc3f
source:
  - path: "src/KeyMouse.Core/ScriptRunner.cs"
    line: 746
    end_line: 752
apis:
  - protocol: rpc
    path: "ScriptRunner.InheritsTarget"
    description:
      zh: >
          该命令是否继承当前目标。
          
      en: >
          Whether a command inherits the current target.
          
---
