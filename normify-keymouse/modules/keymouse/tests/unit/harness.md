---
uid: 4afc5a6b
id: keymouse.tests.unit.harness
parent: keymouse.tests.unit
tags: [test]
name: {zh: "断言与小节输出", en: "Assertions & section output"}
description:
  zh: >
      几十行的极小测试库：Check/Equal/Sequence/Throws/Group，按小节打印 ok/FAIL 并统计总数；不做发现、不做并行、不抛控制流异常。
      
  en: >
      A tiny test harness: Check/Equal/Sequence/Throws/Group, printing ok/FAIL per check with a total count; no discovery, no parallelism, no exception-driven flow.
      
revision: d870b0c5dfd7fe29675863c0288b572a6111a8d4
updated_at: "2026-10-03T07:28:04.465Z"
fingerprint: 1cc617da687ba0e4db55cb590a4aadf6034074f95a278c8b787c4433f3ba23c5
source:
  - path: "tests/KeyMouse.Tests/Harness.cs"
    line: 1
    end_line: 72
apis:
  - protocol: rpc
    path: "Harness.Check"
    description:
      zh: >
          布尔断言（带失败细节）。
          
      en: >
          Boolean assertion with failure detail.
          
  - protocol: rpc
    path: "Harness.Equal"
    description:
      zh: >
          相等断言。
          
      en: >
          Equality assertion.
          
  - protocol: rpc
    path: "Harness.Sequence"
    description:
      zh: >
          字符串序列断言。
          
      en: >
          String-sequence assertion.
          
  - protocol: rpc
    path: "Harness.Throws"
    description:
      zh: >
          应当抛出指定异常。
          
      en: >
          Asserts a specific exception is thrown.
          
---
