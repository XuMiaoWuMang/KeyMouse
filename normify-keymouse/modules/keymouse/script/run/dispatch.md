---
uid: 8f91a3b5
id: keymouse.script.run.dispatch
parent: keymouse.script.run
tags: [script]
name: {zh: "执行循环与逐行作用域", en: "Execution loop & per-line scope"}
description:
  zh: >
      程序计数器 + 循环栈展开 repeat/end；每行重建作用域（--set 变量 + 循环变量）、做替换、处理目标继承、再交出去执行。不复制脚本、不重解析，一条命令一次派发。
      
  en: >
      A program counter plus a loop stack expands repeat/end; each line rebuilds its scope (set variables plus loop variables), substitutes, resolves target inheritance and is handed over to execute. No script copying, no re-parsing, one dispatch per command.
      
revision: e03d53e4c41a8a7220e83123d7f8a44a54ff8dab
updated_at: "2026-10-03T08:15:19.873Z"
fingerprint: 0ef5fdcb1c18d3aaa81d059f6927d97a271aa0ff344d4e213fa1e7d677bd3959
source:
  - path: "ScriptRunner.cs"
    line: 142
    end_line: 305
apis:
  - protocol: rpc
    path: "ScriptRunner.Run"
    description:
      zh: >
          执行入口（接受一个派发委托）。
          
      en: >
          The entry point, taking a dispatch delegate.
          
deps:
  - kind: call
    to: keymouse.script.vars.substitute
    from_api: "rpc:ScriptRunner.Run"
    to_api: "rpc:ScriptRunner.Substitute"
    label: {zh: "逐行变量替换", en: "Substitute per line"}
  - kind: call
    to: keymouse.script.target.extract
    from_api: "rpc:ScriptRunner.Run"
    to_api: "rpc:ScriptRunner.ExtractTargetTokens"
    label: {zh: "提取目标", en: "Extract target"}
  - kind: call
    to: keymouse.script.target.inherit
    from_api: "rpc:ScriptRunner.Run"
    to_api: "rpc:ScriptRunner.InheritsTarget"
    label: {zh: "继承判定", en: "Inheritance rule"}
  - kind: call
    to: keymouse.script.capture
    from_api: "rpc:ScriptRunner.Run"
    to_api: "rpc:ScriptRunner.RunOnce"
    label: {zh: "逐条执行", en: "Execute one line"}
  - kind: call
    to: keymouse.script.wait.parse
    from_api: "rpc:ScriptRunner.Run"
    to_api: "rpc:ScriptRunner.ParseWait"
    label: {zh: "wait 行参数", en: "Wait arguments"}
  - kind: call
    to: keymouse.script.wait.until
    from_api: "rpc:ScriptRunner.Run"
    to_api: "rpc:ScriptRunner.WaitUntil"
    label: {zh: "等待直到满足", en: "Wait until satisfied"}
---
