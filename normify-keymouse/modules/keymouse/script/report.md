---
uid: 1ecf2d3e
id: keymouse.script.report
parent: keymouse.script
tags: [script, report]
name: {zh: "执行报告", en: "Execution report"}
description:
  zh: >
      把报告序列化成 JSON：每条命令的行号、实际执行命令、尝试次数、耗时、注入事件数、退出码、输出与循环变量，以及整份汇总。中文不转义成 \uXXXX（不然报告没法看），报告里的消息文本与终端一致。
      
  en: >
      Serialises the report to JSON: per command the line, effective argv, attempts, duration, injected events, exit code, output and loop variables, plus the summary. Chinese is not escaped into \uXXXX so the file stays readable.
      
revision: 6347fbfbc3d7af8839f5dcc9c35501ce2484bdef
updated_at: "2026-10-03T11:11:56.210Z"
fingerprint: ca45bdaf4b9f154a70f5dbccdacf28399554f02a52725ac899164738ebdf95af
source:
  - path: "src/KeyMouse.Core/ScriptRunner.cs"
    line: 863
    end_line: 883
apis:
  - protocol: file
    path: "<报告>.json"
    description:
      zh: >
          机器可读的执行报告。
          
      en: >
          A machine-readable execution report.
          
  - protocol: rpc
    path: "ScriptRunner.WriteReport"
    description:
      zh: >
          写出 JSON 报告。
          
      en: >
          Writes the JSON report.
          
deps:
  - kind: reference
    to: keymouse.script.model
    from_api: "rpc:ScriptRunner.WriteReport"
    to_api: "rpc:ScriptReport"
    label: {zh: "序列化报告模型", en: "Serialise the model"}
---
