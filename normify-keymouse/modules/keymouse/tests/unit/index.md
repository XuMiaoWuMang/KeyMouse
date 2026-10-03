---
uid: 3feb4f5a
id: keymouse.tests.unit
parent: keymouse.tests
tags: [test]
name: {zh: "单元测试", en: "Unit tests"}
description:
  zh: >
      零依赖控制台程序：不引测试框架、离线可跑、dotnet run 就是全部用法，非零退出码即失败。它已经抳下过真问题（vk:0x5B 从 v1.0 就写在文档里却从未工作）。
      
  en: >
      A dependency-free console program: no test framework, runs offline, `dotnet run` is the whole interface, a non-zero exit code is a failure. It has already caught a real one - vk:0x5B, documented since v1.0 yet never working.
      
revision: 8138caf9efa304f3487fcf5c6320ee4ac39f036b
updated_at: "2026-10-03T10:12:03.456Z"
fingerprint: 2a78fb0e938bdf1ed60e25eaba54f9f94c5398d37f1e0d4af1195a77de2d2b54
source:
  - path: "tests/KeyMouse.Tests/TestEntry.cs"
    line: 1
    end_line: 25
deps:
  - kind: call
    to: keymouse.tests.unit.harness
    from_api: "rpc:ParsingTests.Run"
    to_api: "rpc:Harness.Check"
    label: {zh: "用统一断言", en: "Use the harness"}
---
