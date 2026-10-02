---
uid: 2d8e9fa0
id: keymouse.script.execution-mode
parent: keymouse.script
tags: [script]
name: {zh: "执行模式与计数", en: "Execution mode & counters"}
description:
  zh: >
      脚本执行期间共享的两个开关：演练模式（抑制真实注入）与已发/已抑制事件计数。注入层读它来决定“真发”还是“只记账”。
  en: >
      Two switches shared while a script runs: dry-run (suppress real injection) and the injected/suppressed counters the input layer reads to decide between sending and only counting.
revision: 8d69164cfe5fa0896f595c0bca51f97977ff51b9
updated_at: "2026-10-02T18:00:00Z"
fingerprint: 0ef5fdcb1c18d3aaa81d059f6927d97a271aa0ff344d4e213fa1e7d677bd3959
source:
  - path: "ScriptRunner.cs"
    line: 7
    end_line: 22
apis:
  - protocol: rpc
    path: "ExecutionMode.DryRun"
    description:
      zh: >
          演练开关（不真发）。
      en: >
          Dry-run switch.
  - protocol: rpc
    path: "ExecutionMode.NoteInjected"
    description:
      zh: >
          计数：已发送。
      en: >
          Counts injected events.
  - protocol: rpc
    path: "ExecutionMode.NoteSuppressed"
    description:
      zh: >
          计数：已抑制。
      en: >
          Counts suppressed events.
---
