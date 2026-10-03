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
      
revision: d870b0c5dfd7fe29675863c0288b572a6111a8d4
updated_at: "2026-10-03T07:28:04.462Z"
fingerprint: 0ef5fdcb1c18d3aaa81d059f6927d97a271aa0ff344d4e213fa1e7d677bd3959
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
