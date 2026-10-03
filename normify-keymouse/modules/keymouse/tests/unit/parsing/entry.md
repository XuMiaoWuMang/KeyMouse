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
      
revision: 6347fbfbc3d7af8839f5dcc9c35501ce2484bdef
updated_at: "2026-10-03T11:11:56.215Z"
fingerprint: 36327f9a4054c210af82c13d5b23bc44d4e5d8defd5bf0f6e966adab5530ed35
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
