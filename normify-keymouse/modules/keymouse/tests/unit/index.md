---
uid: 3feb4f5a
id: keymouse.tests.unit
parent: keymouse.tests
tags: [test]
name: {zh: "单元测试", en: "Unit tests"}
description:
  zh: >
      单元测试入口与模块表：`--only 模块[,…]` 只跑改到的那些、`--list` 列出来、不给参数跑全部（功能定稿后的总测试）。九个模块（parsing / windows / probe / locator / region / flow / loops / calls / runner）各自一个文件，互不牵连。
      
  en: >
      The unit-test entry and its module table: --only module[,...] runs just those, --list lists them, and no arguments runs everything (the full test once a feature is settled). Nine modules (parsing, windows, probe, locator, region, flow, loops, calls, runner) each live in one file and do not drag the others along.
      
revision: 1ebae14ff2430b597cc4a1695a71ddf788879db1
updated_at: "2026-10-03T12:49:24.518Z"
fingerprint: 55af18d63fbba8856290532a52e2424e9e5dd7888cf7f1f62422d19357e6cd72
source:
  - path: "tests/KeyMouse.Tests/TestEntry.cs"
deps:
  - kind: call
    to: keymouse.tests.unit.harness
    from_api: "rpc:ParsingTests.Run"
    to_api: "rpc:Harness.Check"
    label: {zh: "用统一断言", en: "Use the harness"}
---
