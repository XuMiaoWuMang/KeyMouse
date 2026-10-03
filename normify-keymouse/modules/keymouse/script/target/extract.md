---
uid: 9d4e5f6a
id: keymouse.script.target.extract
parent: keymouse.script.target
tags: [script, target]
name: {zh: "选择器提取", en: "Selector extraction"}
description:
  zh: >
      从一行里挑出目标选项（--title/--title-exact/--class/--process/--pid/--hwnd/--pick），支持 --flag value 与 --flag=value 两种写法；客户区坐标 -wx/-wy 不算目标选项。
      
  en: >
      Picks the target options out of a line in both --flag value and --flag=value forms; client-area flags like -wx/-wy are deliberately not target options.
      
revision: 6347fbfbc3d7af8839f5dcc9c35501ce2484bdef
updated_at: "2026-10-03T11:11:56.211Z"
fingerprint: ca45bdaf4b9f154a70f5dbccdacf28399554f02a52725ac899164738ebdf95af
source:
  - path: "src/KeyMouse.Core/ScriptRunner.cs"
    line: 717
    end_line: 745
apis:
  - protocol: rpc
    path: "ScriptRunner.ExtractTargetTokens"
    description:
      zh: >
          取出一行的目标选项。
          
      en: >
          Extracts a line's target options.
          
---
