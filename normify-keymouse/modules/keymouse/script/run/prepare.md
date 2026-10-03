---
uid: 7e8092a4
id: keymouse.script.run.prepare
parent: keymouse.script.run
tags: [script]
name: {zh: "读入、分析、校验", en: "Read, analyse, validate"}
description:
  zh: >
      run 的前半段：解析选项 → 读脚本 → 循环结构分析 → 伪命令校验 → 变量预校验 → 初始化报告与打印头部。任何一步不过就在这里返回，命令一条都还没跑。
      
  en: >
      The first half of run: parse options, read the script, analyse loops, validate pseudo-commands and variables, then initialise the report and print the header. Any failure returns here, before a single command runs.
      
revision: 8138caf9efa304f3487fcf5c6320ee4ac39f036b
updated_at: "2026-10-03T10:11:54.008Z"
fingerprint: f561ff1a777b64b465d886e85545f84beb07f3a3d116e9cb8bb0148ac7caf04c
source:
  - path: "ScriptRunner.cs"
    line: 81
    end_line: 141
apis:
  - protocol: rpc
    path: "KeyMouse run <脚本>"
    description:
      zh: >
          执行一个脚本文件（含全量静态检查）。
          
      en: >
          Runs a script file after the full static check.
          
  - protocol: rpc
    path: "KeyMouse run -"
    description:
      zh: >
          从 stdin 读脚本。
          
      en: >
          Reads the script from stdin.
          
deps:
  - kind: call
    to: keymouse.script.options
    from_api: "rpc:KeyMouse run <脚本>"
    to_api: "rpc:ScriptRunner.ParseOptions"
    label: {zh: "解析选项", en: "Parse options"}
  - kind: call
    to: keymouse.script.read
    from_api: "rpc:KeyMouse run <脚本>"
    to_api: "rpc:ScriptRunner.ReadScript"
    label: {zh: "读脚本", en: "Read the script"}
  - kind: call
    to: keymouse.script.parse.lines
    from_api: "rpc:KeyMouse run <脚本>"
    to_api: "rpc:ScriptRunner.Parse"
    label: {zh: "分行", en: "Split lines"}
  - kind: call
    to: keymouse.script.loop.analyze
    from_api: "rpc:KeyMouse run <脚本>"
    to_api: "rpc:ScriptRunner.AnalyzeLoops"
    label: {zh: "循环结构", en: "Loop structure"}
  - kind: call
    to: keymouse.script.vars.validate
    from_api: "rpc:KeyMouse run <脚本>"
    to_api: "rpc:ScriptRunner.ValidateVariables"
    label: {zh: "变量预校验", en: "Validate variables"}
  - kind: call
    to: keymouse.script.pseudo
    from_api: "rpc:KeyMouse run <脚本>"
    to_api: "rpc:ScriptRunner.ValidatePseudoCommands"
    label: {zh: "伪命令校验", en: "Check pseudo-commands"}
---
