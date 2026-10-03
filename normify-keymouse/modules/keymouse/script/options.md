---
uid: df4e5f6a
id: keymouse.script.options
parent: keymouse.script
tags: [script, parsing]
name: {zh: "run 选项解析", en: "run option parsing"}
description:
  zh: >
      脚本与流程运行器的选项：路径、延迟、`--keep-going`、`--dry-run`、`--echo`、`--retry[--delay]`、`--report`、`--set`，以及 `--allow-restore`（允许还原最小化窗口，默认关——还原是在改用户的桌面，所以要明说）。同一份选项既服务文本脚本，也服务流程。
      
  en: >
      Options for the script and flow runners: path, delay, --keep-going, --dry-run, --echo, --retry[--delay], --report, --set, and --allow-restore (may a minimized window be restored? off by default, because that changes someone desktop). The same options serve the text script and the flow.
      
revision: 96c306061d63c034a01c2369e4abc295251e2f35
updated_at: "2026-10-03T13:37:48.971Z"
fingerprint: 460dacfdc9a8f322adac95634e9cc16d4d25d40c8aba413878ef14a5332d4df8
source:
  - path: "src/KeyMouse.Core/ScriptRunner.cs"
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
