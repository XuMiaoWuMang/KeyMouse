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
      
revision: 8138caf9efa304f3487fcf5c6320ee4ac39f036b
updated_at: "2026-10-03T10:11:54.008Z"
fingerprint: f561ff1a777b64b465d886e85545f84beb07f3a3d116e9cb8bb0148ac7caf04c
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
