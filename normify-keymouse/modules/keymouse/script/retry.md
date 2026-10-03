---
uid: fcae1b2c
id: keymouse.script.retry
parent: keymouse.script
tags: [script, error]
name: {zh: "安全重试策略", en: "Safe retry policy"}
description:
  zh: >
      只有退出码 3/4/5 可以重试——这三个码能证明“一个字节都没发”；1 可能已经发了一半，永远不重试。这行“只重试可证明没发过的失败”是整个重试特性的安全性依据。
      
  en: >
      Only exit codes 3/4/5 are retryable, because those provably sent nothing; code 1 may have sent half an action and is never retried. That one rule is what makes retry safe at all.
      
revision: 8138caf9efa304f3487fcf5c6320ee4ac39f036b
updated_at: "2026-10-03T10:11:54.008Z"
fingerprint: f561ff1a777b64b465d886e85545f84beb07f3a3d116e9cb8bb0148ac7caf04c
source:
  - path: "ScriptRunner.cs"
    line: 834
    end_line: 835
apis:
  - protocol: rpc
    path: "ScriptRunner.IsRetryable"
    description:
      zh: >
          该退出码能否安全重试。
          
      en: >
          Whether an exit code may be retried.
          
---
