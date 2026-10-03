---
uid: ae1b2c3d
id: keymouse.script.model
parent: keymouse.script
tags: [script, model]
name: {zh: "脚本数据模型", en: "Script data model"}
description:
  zh: >
      三个纯数据类：run 的选项（路径/延时/重试/变量/报告/演练/继续）、单条命令记录（行号/实际命令/尝试次数/注入数/输出/循环变量）与整份报告（总数/成败数/停止行）。
      
  en: >
      Three plain data types: run options, one command record (line, effective command, attempts, injected count, output, loop variables) and the report as a whole.
      
revision: 8138caf9efa304f3487fcf5c6320ee4ac39f036b
updated_at: "2026-10-03T10:11:54.006Z"
fingerprint: f561ff1a777b64b465d886e85545f84beb07f3a3d116e9cb8bb0148ac7caf04c
source:
  - path: "ScriptRunner.cs"
    line: 24
    end_line: 80
apis:
  - protocol: rpc
    path: "ScriptOptions"
    description:
      zh: >
          run 的选项集合。
          
      en: >
          Options for one run.
          
  - protocol: rpc
    path: "CommandRecord"
    description:
      zh: >
          一条命令的执行记录。
          
      en: >
          One command's execution record.
          
  - protocol: rpc
    path: "ScriptReport"
    description:
      zh: >
          整份执行报告。
          
      en: >
          The whole execution report.
          
---
