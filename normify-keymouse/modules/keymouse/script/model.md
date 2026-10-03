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
      
revision: 96c306061d63c034a01c2369e4abc295251e2f35
updated_at: "2026-10-03T13:37:44.316Z"
fingerprint: 460dacfdc9a8f322adac95634e9cc16d4d25d40c8aba413878ef14a5332d4df8
source:
  - path: "src/KeyMouse.Core/ScriptRunner.cs"
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
