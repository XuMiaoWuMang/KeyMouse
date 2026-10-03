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
      
revision: 96c306061d63c034a01c2369e4abc295251e2f35
updated_at: "2026-10-03T13:37:44.315Z"
fingerprint: 460dacfdc9a8f322adac95634e9cc16d4d25d40c8aba413878ef14a5332d4df8
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
