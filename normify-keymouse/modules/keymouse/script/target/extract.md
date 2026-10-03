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
      
revision: 96c306061d63c034a01c2369e4abc295251e2f35
updated_at: "2026-10-03T13:37:44.318Z"
fingerprint: 460dacfdc9a8f322adac95634e9cc16d4d25d40c8aba413878ef14a5332d4df8
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
