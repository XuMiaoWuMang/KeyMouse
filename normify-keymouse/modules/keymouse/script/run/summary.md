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
      
revision: 1b316650150f1540368ee548f7d9992ccaa3239e
updated_at: "2026-10-03T11:27:16.007Z"
fingerprint: 77e8c215319806cea2fd833e231759721bc3125357c377795fe231100c8ddc3f
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
