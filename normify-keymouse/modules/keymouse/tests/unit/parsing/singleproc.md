---
uid: 07192b3d
id: keymouse.tests.unit.parsing.singleproc
parent: keymouse.tests.unit.parsing
tags: [test, invariant]
name: {zh: "单进程派发用例", en: "Single-process dispatch"}
description:
  zh: >
      注入一个假的派发器并数它被调用的次数：循环展开后 5 条命令就是 5 次调用，且派发器从未看到 repeat/end。如果哪天有人改成“每行起一个进程”，这个委托根本不会被调到，用例立刻变红。
      
  en: >
      Injects a fake dispatcher and counts the calls: five expanded commands mean five calls, and the dispatcher never sees repeat/end. If anyone ever made it spawn a process per line, this delegate would never be called.
      
revision: e03d53e4c41a8a7220e83123d7f8a44a54ff8dab
updated_at: "2026-10-03T08:15:19.892Z"
fingerprint: 8ae0988f099cc0964f5efa1371349b46dff1db459c556cf1cf842f00fbdee247
source:
  - path: "tests/KeyMouse.Tests/ParsingTests.cs"
    line: 93
    end_line: 138
apis:
  - protocol: rpc
    path: "SingleProcessExecution 用例"
    description:
      zh: >
          单进程执行的回归保护。
          
      en: >
          Regression cover for single-process execution.
          
deps:
  - kind: reference
    to: keymouse.script.run.dispatch
    from_api: "rpc:SingleProcessExecution 用例"
    to_api: "rpc:ScriptRunner.Run"
    label: {zh: "被测路径", en: "Subject"}
---
