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
      
revision: 6347fbfbc3d7af8839f5dcc9c35501ce2484bdef
updated_at: "2026-10-03T11:11:56.212Z"
fingerprint: ca45bdaf4b9f154a70f5dbccdacf28399554f02a52725ac899164738ebdf95af
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
