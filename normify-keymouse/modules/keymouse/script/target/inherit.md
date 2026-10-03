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
      
revision: 96c306061d63c034a01c2369e4abc295251e2f35
updated_at: "2026-10-03T13:37:44.318Z"
fingerprint: 460dacfdc9a8f322adac95634e9cc16d4d25d40c8aba413878ef14a5332d4df8
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
