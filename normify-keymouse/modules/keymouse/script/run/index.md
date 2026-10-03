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
      
revision: 8138caf9efa304f3487fcf5c6320ee4ac39f036b
updated_at: "2026-10-03T10:11:54.008Z"
fingerprint: f561ff1a777b64b465d886e85545f84beb07f3a3d116e9cb8bb0148ac7caf04c
source:
  - path: "ScriptRunner.cs"
    line: 81
    end_line: 360
---
