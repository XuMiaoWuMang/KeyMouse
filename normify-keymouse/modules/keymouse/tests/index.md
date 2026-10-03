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
      
revision: 6347fbfbc3d7af8839f5dcc9c35501ce2484bdef
updated_at: "2026-10-03T11:11:56.214Z"
fingerprint: 81dae4b00efe6063ea21b685f4a2172e2daa8a30a12bc2f0ee0e2ff6b48bf73f
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
