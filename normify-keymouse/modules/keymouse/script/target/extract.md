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
revision: 8d69164cfe5fa0896f595c0bca51f97977ff51b9
updated_at: "2026-10-02T18:10:00Z"
fingerprint: 0ef5fdcb1c18d3aaa81d059f6927d97a271aa0ff344d4e213fa1e7d677bd3959
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
