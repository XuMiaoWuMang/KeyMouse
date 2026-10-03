---
uid: 7a1f1001
id: keymouse.tests.unit.loops
parent: keymouse.tests.unit
tags: [test, unit]
name: {zh: "循环与变量单测", en: "Loop and variable tests"}
description:
  zh: >
      不需要桌面：重复与遍历的展平（含嵌套相乘、次数 0、未知列表），展平不会改动文档，占位符替换与未定义变量的拒绝，分组不能带 when、缺 times/in/into 的拒绝，以及"read-text 的 into 对它后面的步骤可见"这条顺序规则。
      
  en: >
      No desktop needed: flattening repeats and foreach loops (nested multiplication, zero times, an unknown list), flattening leaving the document untouched, placeholder substitution and the refusal of undefined variables, groups that cannot carry a when, missing times/in/into, and the ordering rule that a read-text into is visible to the steps after it.
      
revision: 69321dbdf0d2f94c72a77144dd1c35e063d13815
updated_at: "2026-10-03T11:41:09.822Z"
fingerprint: ce993c5b52da2a77cf0d139162628103d2a9dc9d34817c90231cba230e4f5b52
source:
  - path: "tests/KeyMouse.Tests/LoopTests.cs"
apis:
  - protocol: rpc
    path: "循环与变量断言组"
    description:
      zh: >
          展平、替换、加载校验。
          
      en: >
          Flattening, substitution, load validation.
          
deps:
  - kind: reference
    to: keymouse.flow.model
    to_api: "rpc:FlowDocument.Load"
    label: {zh: "被测的加载器", en: "Loader under test"}
---
