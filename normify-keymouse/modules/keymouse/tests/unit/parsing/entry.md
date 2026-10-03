---
uid: b2c4d6e8
id: keymouse.tests.unit.parsing.entry
parent: keymouse.tests.unit.parsing
tags: [test]
name: {zh: "用例入口", en: "Case entry"}
description:
  zh: >
      按顺序调用全部用例组并汇总计数；一个用例组抛异常就中止整轮（说明断言之外发生了意外）。
      
  en: >
      Calls every case set in order and totals the counts; an unexpected exception in one set aborts the whole run.
      
revision: 96c306061d63c034a01c2369e4abc295251e2f35
updated_at: "2026-10-03T13:37:44.321Z"
fingerprint: 1263671f94fb9ec06ca4072a554642c69297a717af171eff59c55c582848dc59
source:
  - path: "tests/KeyMouse.Tests/ParsingTests.cs"
    line: 7
    end_line: 21
apis:
  - protocol: rpc
    path: "ParsingTests.Run"
    description:
      zh: >
          跑完所有纯逻辑用例。
          
      en: >
          Runs every desktop-free case.
          
---
