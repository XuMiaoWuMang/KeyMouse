---
uid: bf6a7b8c
id: keymouse.script.pseudo
parent: keymouse.script
tags: [script]
name: {zh: "伪命令静态校验", en: "Pseudo-command validation"}
description:
  zh: >
      sleep 与 waitfor/waitgone 在执行前先校验一遍：次数非法、wait 缺选择器、参数多了少了都在这里报错，避免脚本跑到中间才发现写错。
      
  en: >
      sleep and the wait pseudo-commands are validated before anything runs, so a bad duration or a wait without a selector fails up front instead of mid-script.
      
revision: 8138caf9efa304f3487fcf5c6320ee4ac39f036b
updated_at: "2026-10-03T10:11:54.007Z"
fingerprint: f561ff1a777b64b465d886e85545f84beb07f3a3d116e9cb8bb0148ac7caf04c
source:
  - path: "ScriptRunner.cs"
    line: 753
    end_line: 777
apis:
  - protocol: rpc
    path: "ScriptRunner.ValidatePseudoCommands"
    description:
      zh: >
          逐条校验伪命令写法。
          
      en: >
          Validates each pseudo-command.
          
deps:
  - kind: call
    to: keymouse.script.wait.parse
    from_api: "rpc:ScriptRunner.ValidatePseudoCommands"
    to_api: "rpc:ScriptRunner.ParseWait"
    label: {zh: "校验 wait 参数", en: "Check wait arguments"}
---
