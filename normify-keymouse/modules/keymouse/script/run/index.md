---
uid: c81f4e93
id: keymouse.script.run
parent: keymouse.script
tags: [script]
name: {zh: "主流程与逐条派发", en: "Main flow & dispatch"}
description:
  zh: >
      run 的控制流：读→分析→逐条执行。维护程序计数器、循环栈、当前目标与逐行作用域，打印进度（含迭代标注与继承标注），首错即停或 --keep-going，并按第一条失败命令的退出码返回。所有命令都在本进程内通过调用方注入的派发委托执行。
      
  en: >
      The control flow of run: read, analyse, then execute line by line with a program counter, loop stack, current target and per-line scope; it prints progress, stops at the first failure unless --keep-going, and returns that exit code. Every command runs in this process through a dispatch delegate handed in by the caller.
      
revision: 6347fbfbc3d7af8839f5dcc9c35501ce2484bdef
updated_at: "2026-10-03T11:11:56.211Z"
fingerprint: ca45bdaf4b9f154a70f5dbccdacf28399554f02a52725ac899164738ebdf95af
source:
  - path: "src/KeyMouse.Core/ScriptRunner.cs"
    line: 81
    end_line: 360
---
