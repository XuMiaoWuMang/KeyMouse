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
      
revision: d870b0c5dfd7fe29675863c0288b572a6111a8d4
updated_at: "2026-10-03T07:28:04.458Z"
fingerprint: 0ef5fdcb1c18d3aaa81d059f6927d97a271aa0ff344d4e213fa1e7d677bd3959
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
