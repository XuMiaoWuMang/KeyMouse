---
uid: d4e6f80a
id: keymouse.tests.unit.parsing.options
parent: keymouse.tests.unit.parsing
tags: [test]
name: {zh: "选项与按键名用例", en: "Options & key names"}
description:
  zh: >
      全局选项提取（含 --flag=value、选择器拼装、悬空值报错）、各命令自己的选项，以及按键名映射（含 vk: 逃生口与未知键报错）。
      
  en: >
      Global option extraction (inline values, selector assembly, a dangling value being an error), per-command options, and the key-name map including the vk: hatch.
      
revision: 6347fbfbc3d7af8839f5dcc9c35501ce2484bdef
updated_at: "2026-10-03T11:11:56.216Z"
fingerprint: 36327f9a4054c210af82c13d5b23bc44d4e5d8defd5bf0f6e966adab5530ed35
source:
  - path: "tests/KeyMouse.Tests/ParsingTests.cs"
    line: 320
    end_line: 403
apis:
  - protocol: rpc
    path: "GlobalOptions / CommandOptions / KeyNames 用例"
    description:
      zh: >
          选项解析与按键名的断言集。
          
      en: >
          Assertions for option parsing and key names.
          
deps:
  - kind: reference
    to: keymouse.cli.options
    from_api: "rpc:GlobalOptions / CommandOptions / KeyNames 用例"
    to_api: "rpc:ExtractGlobalOptions"
    label: {zh: "被测对象", en: "Subject"}
  - kind: reference
    to: keymouse.keys
    from_api: "rpc:GlobalOptions / CommandOptions / KeyNames 用例"
    to_api: "rpc:KeyMap.Resolve"
    label: {zh: "按键名表", en: "Key names"}
---
