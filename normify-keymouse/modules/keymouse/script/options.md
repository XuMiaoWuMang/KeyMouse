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
      
revision: 8138caf9efa304f3487fcf5c6320ee4ac39f036b
updated_at: "2026-10-03T10:11:54.006Z"
fingerprint: f561ff1a777b64b465d886e85545f84beb07f3a3d116e9cb8bb0148ac7caf04c
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
