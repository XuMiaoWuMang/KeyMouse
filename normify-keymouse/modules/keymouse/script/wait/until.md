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
      
revision: d870b0c5dfd7fe29675863c0288b572a6111a8d4
updated_at: "2026-10-03T07:28:04.462Z"
fingerprint: 0ef5fdcb1c18d3aaa81d059f6927d97a271aa0ff344d4e213fa1e7d677bd3959
source:
  - path: "ScriptRunner.cs"
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
