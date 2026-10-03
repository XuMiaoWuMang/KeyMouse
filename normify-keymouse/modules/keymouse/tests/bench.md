---
uid: a05bb0c1
id: keymouse.tests.bench
parent: keymouse.tests
tags: [test, perf]
name: {zh: "解析与执行基准", en: "Parse & execution bench"}
description:
  zh: >
      用测量代替直觉：同样 2000 条命令，文本分词 2ms、JSON 解析 3.5ms，而带选择器执行 2000 条要 6s——顺带给出一条选择器命令的成本拆解（枚举、进程名查询）。它的存在让“要不要换 JSON 格式”这种问题有一个数字答案。
      
  en: >
      Measures instead of guessing: for the same 2000 commands, text tokenising takes 2 ms, JSON parsing 3.5 ms, and executing them with selectors takes 6 s - plus a breakdown of what a selector command actually costs.
      
revision: 6347fbfbc3d7af8839f5dcc9c35501ce2484bdef
updated_at: "2026-10-03T11:11:56.213Z"
fingerprint: ed5e526893e794b5541f533dd48db06c72ffe222c39bb37c6116552055c1ab49
source:
  - path: "tests/KeyMouse.Tests/ParseBench.cs"
    line: 1
    end_line: 143
apis:
  - protocol: rpc
    path: "dotnet run -- bench"
    description:
      zh: >
          跑解析与执行基准。
          
      en: >
          Runs the parse and execution benchmark.
          
deps:
  - kind: call
    to: keymouse.script.parse.tokenize
    from_api: "rpc:dotnet run -- bench"
    to_api: "rpc:ScriptRunner.Tokenize"
    label: {zh: "测分词", en: "Measure tokeniser"}
  - kind: call
    to: keymouse.cli.main
    from_api: "rpc:dotnet run -- bench"
    to_api: "rpc:keymouse <命令组>"
    label: {zh: "测真实命令", en: "Measure a real command"}
---
