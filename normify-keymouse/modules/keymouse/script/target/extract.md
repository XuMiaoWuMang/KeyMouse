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
      
revision: 8138caf9efa304f3487fcf5c6320ee4ac39f036b
updated_at: "2026-10-03T10:11:54.008Z"
fingerprint: f561ff1a777b64b465d886e85545f84beb07f3a3d116e9cb8bb0148ac7caf04c
source:
  - path: "ScriptRunner.cs"
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
