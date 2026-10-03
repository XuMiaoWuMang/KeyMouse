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
      
revision: 96c306061d63c034a01c2369e4abc295251e2f35
updated_at: "2026-10-03T13:37:48.971Z"
fingerprint: 1263671f94fb9ec06ca4072a554642c69297a717af171eff59c55c582848dc59
source:
  - path: "tests/KeyMouse.Tests/ParsingTests.cs"
    line: 1
    end_line: 403
---
