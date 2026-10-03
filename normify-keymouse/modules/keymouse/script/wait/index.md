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
      
revision: 6347fbfbc3d7af8839f5dcc9c35501ce2484bdef
updated_at: "2026-10-03T11:11:56.212Z"
fingerprint: ca45bdaf4b9f154a70f5dbccdacf28399554f02a52725ac899164738ebdf95af
source:
  - path: "src/KeyMouse.Core/ScriptRunner.cs"
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
