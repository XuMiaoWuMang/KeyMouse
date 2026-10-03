---
uid: 92a3b4c5
id: keymouse.tests
parent: keymouse
tags: [test]
name: {zh: "测试体系", en: "Test suite"}
description:
  zh: >
      两层护栏：零依赖的单元测试（逻辑，不需要桌面）与真实桌面冒烟（用自己启动的靶子窗口跑完整链路）。另含文档链接检查与解析/执行基准。
      
  en: >
      Two layers of protection: a dependency-free unit suite for logic, and a desktop smoke suite that drives the project's own target window end to end. Plus a docs-link check and a parse/execution benchmark.
      
revision: 96c306061d63c034a01c2369e4abc295251e2f35
updated_at: "2026-10-03T13:37:44.320Z"
fingerprint: 51cfbc1fb112f024ddf0d6b1ad029f4dcb24ab60e2a281d3bff2c44f077e0825
source:
  - path: "tests/KeyMouse.Tests/TestEntry.cs"
  - path: "tests/KeyMouse.Tests/Harness.cs"
  - path: "tests/KeyMouse.Tests/ParsingTests.cs"
  - path: "tests/KeyMouse.Tests/WindowTests.cs"
  - path: "tests/KeyMouse.Tests/ParseBench.cs"
  - path: "tests/KeyMouse.SmokeTarget/Program.cs"
  - path: "tests/smoke.ps1"
  - path: "tests/check-docs.ps1"
---
