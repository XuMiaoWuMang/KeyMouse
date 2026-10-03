---
uid: eb9dae1f
id: keymouse.script.wait.until
parent: keymouse.script.wait
tags: [script, wait]
name: {zh: "等待循环", en: "Wait loop"}
description:
  zh: >
      以固定间隔重复调用一个断言函数，直到为真或超时，并返回耗时；断言本身走完整的窗口解析与闸门判定。
      
  en: >
      Repeats an assertion at a fixed interval until it holds or the timeout expires, reporting the elapsed time; the assertion itself goes through the full window resolution and gate.
      
revision: 1b316650150f1540368ee548f7d9992ccaa3239e
updated_at: "2026-10-03T11:27:16.009Z"
fingerprint: 77e8c215319806cea2fd833e231759721bc3125357c377795fe231100c8ddc3f
source:
  - path: "src/KeyMouse.Core/ScriptRunner.cs"
    line: 818
    end_line: 833
apis:
  - protocol: rpc
    path: "ScriptRunner.WaitUntil"
    description:
      zh: >
          轮询直到满足或超时。
          
      en: >
          Polls until satisfied or timed out.
          
deps:
  - kind: call
    to: keymouse.window.resolve
    from_api: "rpc:ScriptRunner.WaitUntil"
    to_api: "rpc:WindowResolver.Resolve"
    label: {zh: "用闸门判定", en: "Judge via the gate"}
---
