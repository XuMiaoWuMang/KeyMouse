---
uid: e5f7091b
id: keymouse.tests.unit.parsing.script
parent: keymouse.tests.unit.parsing
tags: [test]
name: {zh: "脚本选项、变量与重试用例", en: "Run options, variables & retry"}
description:
  zh: >
      run 的选项解析、${} 替换与预校验（未定义、未闭合、同一 token 多次引用），以及“只有 3/4/5 可重试”这条安全规则。
      
  en: >
      run's option parsing, ${} substitution and its upfront check (undefined, unterminated, repeated in one token), and the rule that only 3/4/5 may be retried.
      
revision: 96c306061d63c034a01c2369e4abc295251e2f35
updated_at: "2026-10-03T13:37:44.322Z"
fingerprint: 1263671f94fb9ec06ca4072a554642c69297a717af171eff59c55c582848dc59
source:
  - path: "tests/KeyMouse.Tests/ParsingTests.cs"
    line: 170
    end_line: 253
apis:
  - protocol: rpc
    path: "RunOptions / RetryAndVariables 用例"
    description:
      zh: >
          脚本选项、变量与重试策略的断言集。
          
      en: >
          Assertions for script options, variables and retry policy.
          
deps:
  - kind: reference
    to: keymouse.script.vars.substitute
    from_api: "rpc:RunOptions / RetryAndVariables 用例"
    to_api: "rpc:ScriptRunner.Substitute"
    label: {zh: "被测对象", en: "Subject"}
  - kind: reference
    to: keymouse.script.retry
    from_api: "rpc:RunOptions / RetryAndVariables 用例"
    to_api: "rpc:ScriptRunner.IsRetryable"
    label: {zh: "重试策略", en: "Retry policy"}
---
