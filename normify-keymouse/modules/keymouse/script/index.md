---
uid: 6f708192
id: keymouse.script
parent: keymouse
tags: [script]
name: {zh: "脚本执行器", en: "Script runner"}
description:
  zh: >
      run 命令的实现：读脚本 → 分词与结构分析（含 repeat/end 循环）→ 变量与目标继承 → 在一个进程内逐条派发，支持 --dry-run、安全重试、--keep-going 与 JSON 报告。所有静态错误都在第一条命令执行前带行号报出。
      
  en: >
      Implements `run`: read, tokenize and analyse a script (including repeat/end loops), expand variables and inherited targets, then dispatch every line inside one process, with --dry-run, safe retry, --keep-going and a JSON report.
      
revision: 6347fbfbc3d7af8839f5dcc9c35501ce2484bdef
updated_at: "2026-10-03T11:11:56.207Z"
fingerprint: ca45bdaf4b9f154a70f5dbccdacf28399554f02a52725ac899164738ebdf95af
source:
  - path: "src/KeyMouse.Core/ScriptRunner.cs"
    line: 1
    end_line: 892
deps:
  - kind: call
    to: keymouse.script.read
    label: {zh: "读脚本", en: "Read the script"}
  - kind: call
    to: keymouse.script.parse.lines
    label: {zh: "分行", en: "Split into lines"}
  - kind: call
    to: keymouse.script.loop.analyze
    label: {zh: "循环结构分析", en: "Analyse loop structure"}
  - kind: call
    to: keymouse.script.vars.validate
    label: {zh: "变量预校验", en: "Validate variables"}
  - kind: call
    to: keymouse.script.target.extract
    label: {zh: "每行提取目标", en: "Extract per-line target"}
  - kind: call
    to: keymouse.script.capture
    label: {zh: "逐条执行", en: "Execute each line"}
  - kind: call
    to: keymouse.script.report
    label: {zh: "写报告", en: "Write the report"}
---
