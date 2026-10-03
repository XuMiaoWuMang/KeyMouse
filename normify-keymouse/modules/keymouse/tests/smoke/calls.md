---
uid: 7a1f2003
id: keymouse.tests.smoke.calls
parent: keymouse.tests.smoke
tags: [test, desktop]
name: {zh: "子流程冒烟", en: "Subflow smoke"}
description:
  zh: >
      真桌面上跑一份会 `call` 子流程的流程：循环两轮各调一次、`vars` 把当轮的值传进子流程、子流程自己的默认值不被覆盖、子流程读到的内容经 `export` 回到调用者并被打印出来；再把 `export` 去掉，确认那份捕获在调用者这边**看不见**（退出码 2）。数据用 ASCII：靶子小字号下复杂汉字读不准，那是读屏能力的事。
      
  en: >
      On a real desktop: a flow that calls a subflow twice from a loop, with vars carrying each round in, the subflow own default left intact, and what the subflow read arriving back through export and typed out by the caller; then export removed to prove that capture is invisible outside (exit 2). The data is ASCII because complicated CJK glyphs at the target font size are not readable - that belongs to the OCR story, not this one.
      
revision: 1ebae14ff2430b597cc4a1695a71ddf788879db1
updated_at: "2026-10-03T12:49:24.516Z"
fingerprint: cff0f77e926c0724e33c0b1fd8f1d67948e97cfa5dd727d4eb4008749677ab9f
source:
  - path: "tests/smoke/calls.ps1"
apis:
  - protocol: rpc
    path: "子流程冒烟断言组"
    description:
      zh: >
          内联执行、vars、export 与作用域不外泄。
          
      en: >
          Inline run, vars, export and capture containment.
          
deps:
  - kind: call
    to: keymouse.flow.runner
    to_api: "rpc:FlowRunner.Run"
    label: {zh: "执行主流程", en: "Run the main flow"}
  - kind: reference
    to: keymouse.tests.smoke.common
    to_api: "rpc:Check"
    label: {zh: "断言与靶子", en: "Assertions and target"}
---
