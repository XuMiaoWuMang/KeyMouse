---
uid: 5b0d6b7c
id: keymouse.tests.unit.parsing
parent: keymouse.tests.unit
tags: [test]
name: {zh: "解析与脚本用例", en: "Parsing & script cases"}
description:
  zh: >
      不需要桌面的全部逻辑断言：分词器、全局选项、按键名、run 选项、重试策略、变量替换与预校验、循环结构分析与展开计数、目标继承。约 400 行里藏着几次真实回归（变量没定义时漏到下游、循环嵌套重名）。
      
  en: >
      Every logic assertion that needs no desktop: tokeniser, global options, key names, run options, retry policy, variable substitution and pre-checks, loop analysis and expansion counts, target inheritance.
      
revision: d870b0c5dfd7fe29675863c0288b572a6111a8d4
updated_at: "2026-10-03T07:28:04.466Z"
fingerprint: 8ae0988f099cc0964f5efa1371349b46dff1db459c556cf1cf842f00fbdee247
source:
  - path: "tests/KeyMouse.Tests/ParsingTests.cs"
    line: 1
    end_line: 403
---
