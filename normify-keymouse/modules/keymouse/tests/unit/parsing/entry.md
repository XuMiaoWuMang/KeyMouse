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
      
revision: d870b0c5dfd7fe29675863c0288b572a6111a8d4
updated_at: "2026-10-03T07:28:04.466Z"
fingerprint: 8ae0988f099cc0964f5efa1371349b46dff1db459c556cf1cf842f00fbdee247
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
