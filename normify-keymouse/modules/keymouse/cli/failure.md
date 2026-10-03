---
uid: d2e3f4a5
id: keymouse.cli.failure
parent: keymouse.cli
tags: [error, contract]
name: {zh: "错误类型与退出码", en: "Failure type & exit codes"}
description:
  zh: >
      贯穿全项目的错误契约：CommandFailure 携带退出码（2 参数错、3 无匹配、4 目标不可用、5 焦点验证失败），Fail 统一以「错误：」前缀写 stderr。退出码本身是承诺：3/4/5 意味着一个字节都没发。
      
  en: >
      The error contract used everywhere: CommandFailure carries an exit code (2 usage, 3 no match, 4 unusable target, 5 focus failed) and Fail writes it to stderr with a fixed prefix. The code is a promise: 3/4/5 mean nothing was sent.
      
revision: 96c306061d63c034a01c2369e4abc295251e2f35
updated_at: "2026-10-03T13:37:44.310Z"
fingerprint: 76d50b8e49ed3d7a4bc46bb2652373a2bac9af4793c9742a6c6ef703b35ca71c
source:
  - path: "src/KeyMouse.Core/WindowFocus.cs"
    line: 53
    end_line: 58
  - path: "src/KeyMouse.Core/Commands.cs"
    line: 651
    end_line: 656
apis:
  - protocol: rpc
    path: "CommandFailure"
    description:
      zh: >
          带退出码的业务异常。
          
      en: >
          Domain exception carrying an exit code.
          
  - protocol: rpc
    path: "KeyMouse.Fail"
    description:
      zh: >
          统一错误输出（错误：…）。
          
      en: >
          The single error output path.
          
---
