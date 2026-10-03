---
uid: cf7b8c9d
id: keymouse.script.wait
parent: keymouse.script
tags: [script, wait]
name: {zh: "等待伪命令", en: "Wait pseudo-commands"}
description:
  zh: >
      waitfor / waitgone：等到有（或没有）一个通过闸门的窗口匹配，默认 5000ms 超时 / 200ms 间隔；超时退出码 3。它们是这个工具的条件语句——用等待断言代替分支。
      
  en: >
      waitfor / waitgone wait until a gate-passing window does (or does not) match, defaulting to 5000 ms timeout and 200 ms interval, and exit 3 on timeout. They are this tool's conditionals: assert by waiting.
      
revision: 8138caf9efa304f3487fcf5c6320ee4ac39f036b
updated_at: "2026-10-03T10:11:54.009Z"
fingerprint: f561ff1a777b64b465d886e85545f84beb07f3a3d116e9cb8bb0148ac7caf04c
source:
  - path: "ScriptRunner.cs"
    line: 778
    end_line: 833
deps:
  - kind: call
    to: keymouse.script.wait.parse
    label: {zh: "解析参数", en: "Parse arguments"}
  - kind: call
    to: keymouse.script.wait.until
    label: {zh: "等到满足", en: "Wait until satisfied"}
---
