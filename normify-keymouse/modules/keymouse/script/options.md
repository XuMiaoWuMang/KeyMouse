---
uid: df4e5f6a
id: keymouse.script.options
parent: keymouse.script
tags: [script, parsing]
name: {zh: "run 选项解析", en: "run option parsing"}
description:
  zh: >
      run 自己的选项：--delay / --keep-going / --dry-run / --echo / --retry / --retry-delay / --set 名=值 / --report 文件。--set 的值允许含 “=”（只按第一个等号切），并拒绝把窗口选择器挂在 run 上。
      
  en: >
      The options run itself takes: delay, keep-going, dry-run, echo, retry, retry-delay, --set and --report. A --set value may contain '='; a window selector on run itself is refused.
      
revision: d870b0c5dfd7fe29675863c0288b572a6111a8d4
updated_at: "2026-10-03T07:28:04.457Z"
fingerprint: 0ef5fdcb1c18d3aaa81d059f6927d97a271aa0ff344d4e213fa1e7d677bd3959
source:
  - path: "ScriptRunner.cs"
    line: 384
    end_line: 451
apis:
  - protocol: rpc
    path: "ScriptRunner.ParseOptions"
    description:
      zh: >
          解析 run 的选项。
          
      en: >
          Parses run's options.
          
  - protocol: rpc
    path: "KeyMouse run [选项]"
    description:
      zh: >
          脚本调度的全部开关。
          
      en: >
          Every scheduling switch for a script.
          
---
