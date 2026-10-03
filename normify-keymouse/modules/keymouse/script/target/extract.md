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
      
revision: 1b316650150f1540368ee548f7d9992ccaa3239e
updated_at: "2026-10-03T11:27:16.007Z"
fingerprint: 77e8c215319806cea2fd833e231759721bc3125357c377795fe231100c8ddc3f
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
