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
      
revision: e03d53e4c41a8a7220e83123d7f8a44a54ff8dab
updated_at: "2026-10-03T08:15:19.874Z"
fingerprint: 0ef5fdcb1c18d3aaa81d059f6927d97a271aa0ff344d4e213fa1e7d677bd3959
source:
  - path: "ScriptRunner.cs"
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
