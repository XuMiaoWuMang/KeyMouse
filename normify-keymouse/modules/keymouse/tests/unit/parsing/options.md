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
      
revision: e03d53e4c41a8a7220e83123d7f8a44a54ff8dab
updated_at: "2026-10-03T08:15:19.892Z"
fingerprint: 8ae0988f099cc0964f5efa1371349b46dff1db459c556cf1cf842f00fbdee247
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
