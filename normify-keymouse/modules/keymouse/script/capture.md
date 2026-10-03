---
uid: cf3d4e5f
id: keymouse.script.capture
parent: keymouse.script
tags: [script, error]
name: {zh: "子命令输出捕获与重试", en: "Output capture & retry"}
description:
  zh: >
      执行一条命令并捕获它的 stdout/stderr（不是直接往终端喷），供日志与 JSON 报告使用；同时实现安全重试：只有退出码 3/4/5（可证明一个字节都没发）才会重试，1（可能发了一半）永不重试。
      
  en: >
      Runs one command while capturing its stdout/stderr for the log and the JSON report, and implements safe retry: only exit codes 3/4/5 - provably nothing sent - are retried, never code 1.
      
revision: 1b316650150f1540368ee548f7d9992ccaa3239e
updated_at: "2026-10-03T11:27:16.004Z"
fingerprint: 77e8c215319806cea2fd833e231759721bc3125357c377795fe231100c8ddc3f
source:
  - path: "src/KeyMouse.Core/ScriptRunner.cs"
    line: 361
    end_line: 383
apis:
  - protocol: rpc
    path: "ScriptRunner.RunOnce"
    description:
      zh: >
          执行一条命令并捕获输出。
          
      en: >
          Runs one command and captures its output.
          
deps:
  - kind: call
    to: keymouse.script.execution-mode
    from_api: "rpc:ScriptRunner.RunOnce"
    to_api: "rpc:ExecutionMode.NoteInjected"
    label: {zh: "读注入计数", en: "Read injected count"}
---
