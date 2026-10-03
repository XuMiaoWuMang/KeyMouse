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
      
revision: 6347fbfbc3d7af8839f5dcc9c35501ce2484bdef
updated_at: "2026-10-03T11:11:56.207Z"
fingerprint: ca45bdaf4b9f154a70f5dbccdacf28399554f02a52725ac899164738ebdf95af
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
