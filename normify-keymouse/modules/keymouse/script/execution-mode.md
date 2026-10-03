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
      
revision: 8138caf9efa304f3487fcf5c6320ee4ac39f036b
updated_at: "2026-10-03T10:11:54.005Z"
fingerprint: f561ff1a777b64b465d886e85545f84beb07f3a3d116e9cb8bb0148ac7caf04c
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
