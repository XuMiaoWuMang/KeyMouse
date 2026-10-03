---
uid: 2fda3e4f
id: keymouse.script.display
parent: keymouse.script
tags: [script, rendering]
name: {zh: "回显与错误输出", en: "Echo & error output"}
description:
  zh: >
      把 token 重新拼回可读文本（带空格的值加引号，日志才能无歧义地复现），以及脚本层面的错误输出。
      
  en: >
      Reassembles tokens into readable text, quoting values that contain spaces so the log can be replayed unambiguously, plus the runner's own error output.
      
revision: 1b316650150f1540368ee548f7d9992ccaa3239e
updated_at: "2026-10-03T11:27:16.004Z"
fingerprint: 77e8c215319806cea2fd833e231759721bc3125357c377795fe231100c8ddc3f
source:
  - path: "src/KeyMouse.Core/ScriptRunner.cs"
    line: 884
    end_line: 892
apis:
  - protocol: rpc
    path: "ScriptRunner.Quote"
    description:
      zh: >
          token→可读文本（必要时加引号）。
          
      en: >
          Token to readable text.
          
  - protocol: rpc
    path: "ScriptRunner.Fail"
    description:
      zh: >
          脚本错误输出。
          
      en: >
          The runner's error output.
          
---
