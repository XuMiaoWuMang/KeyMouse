---
uid: a1b3c5d7
id: keymouse.script.run.summary
parent: keymouse.script.run
tags: [script]
name: {zh: "汇总、退出码与报告", en: "Summary, exit code & report"}
description:
  zh: >
      收尾：演练模式提示抑制了多少事件、算出退出码（第一条失败命令的码）、写 JSON 报告、失败时逐条列出行号与命令。
      
  en: >
      The tail end: dry-run reports how many events were suppressed, the exit code is taken from the first failure, the JSON report is written, and failures are listed with their line numbers.
      
revision: 96c306061d63c034a01c2369e4abc295251e2f35
updated_at: "2026-10-03T13:37:44.318Z"
fingerprint: 460dacfdc9a8f322adac95634e9cc16d4d25d40c8aba413878ef14a5332d4df8
source:
  - path: "src/KeyMouse.Core/ScriptRunner.cs"
    line: 334
    end_line: 360
apis:
  - protocol: rpc
    path: "退出码与失败清单"
    description:
      zh: >
          返回首错码并列出失败行。
          
      en: >
          Returns the first failure code and lists the failing lines.
          
deps:
  - kind: call
    to: keymouse.script.report
    from_api: "rpc:退出码与失败清单"
    to_api: "rpc:ScriptRunner.WriteReport"
    label: {zh: "写 JSON 报告", en: "Write the report"}
---
