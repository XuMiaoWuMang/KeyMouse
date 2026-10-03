---
uid: 3feb4f5a
id: keymouse.tests.unit
parent: keymouse.tests
tags: [test]
name: {zh: "单元测试", en: "Unit tests"}
description:
  zh: >
      单元测试入口与模块表：`--only 模块[,…]` 只跑改到的那些、`--list` 列出来、不给参数跑全部（功能定稿后的总测试）。八个模块（parsing / windows / probe / locator / region / flow / loops / runner）各自一个文件，互不牵连——改哪个模块就跑哪个。
      
  en: >
      The unit-test entry and its module table: `--only module[,...]` runs just those, `--list` lists them, and no arguments runs everything (the full test once a feature is settled). Eight modules (parsing, windows, probe, locator, region, flow, loops, runner) each live in one file and do not drag the others along.
      
revision: bc06440df10935e9e8479ecad7b52098b48df7fd
updated_at: "2026-10-03T12:19:12.570Z"
fingerprint: fde95f99e09979a2a6bf827888637ecb816df92ceb1abe20b59dc36db3b2051a
source:
  - path: "tests/KeyMouse.Tests/TestEntry.cs"
deps:
  - kind: call
    to: keymouse.tests.unit.harness
    from_api: "rpc:ParsingTests.Run"
    to_api: "rpc:Harness.Check"
    label: {zh: "用统一断言", en: "Use the harness"}
---
