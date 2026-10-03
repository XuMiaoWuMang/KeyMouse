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
      
revision: d870b0c5dfd7fe29675863c0288b572a6111a8d4
updated_at: "2026-10-03T07:28:04.461Z"
fingerprint: 0ef5fdcb1c18d3aaa81d059f6927d97a271aa0ff344d4e213fa1e7d677bd3959
source:
  - path: "ScriptRunner.cs"
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
