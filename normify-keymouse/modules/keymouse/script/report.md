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
      
revision: 96c306061d63c034a01c2369e4abc295251e2f35
updated_at: "2026-10-03T13:37:44.317Z"
fingerprint: 460dacfdc9a8f322adac95634e9cc16d4d25d40c8aba413878ef14a5332d4df8
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
