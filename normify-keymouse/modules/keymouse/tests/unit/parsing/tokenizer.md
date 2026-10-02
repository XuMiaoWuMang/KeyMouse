---
uid: c3d5e7f9
id: keymouse.tests.unit.parsing.tokenizer
parent: keymouse.tests.unit.parsing
tags: [test]
name: {zh: "分词与行语法用例", en: "Tokeniser & line cases"}
description:
  zh: >
      引号、转义、行内注释、KeyMouse 前缀容忍、行号保留——脚本语法那一层的行为断言。
  en: >
      Quotes, escapes, trailing comments, tolerating a copied KeyMouse prefix, keeping line numbers: the behaviour of the script-syntax layer.
revision: 8d69164cfe5fa0896f595c0bca51f97977ff51b9
updated_at: "2026-10-02T18:40:00Z"
fingerprint: 8ae0988f099cc0964f5efa1371349b46dff1db459c556cf1cf842f00fbdee247
source:
  - path: "tests/KeyMouse.Tests/ParsingTests.cs"
    line: 286
    end_line: 319
apis:
  - protocol: rpc
    path: "Tokenizer / ScriptLines 用例"
    description:
      zh: >
          分词器与行处理的断言集。
      en: >
          Assertions for the tokeniser and line handling.
deps:
  - kind: reference
    to: keymouse.script.parse.tokenize
    from_api: "rpc:Tokenizer / ScriptLines 用例"
    to_api: "rpc:ScriptRunner.Tokenize"
    label: {zh: "被测对象", en: "Subject"}
---
