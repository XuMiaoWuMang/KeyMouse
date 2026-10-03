---
uid: 3a4c5e60
id: keymouse.tests.smoke.safety
parent: keymouse.tests.smoke
tags: [test, desktop]
name: {zh: "重试、报告、等待与禁用窗口", en: "Retry, report, waits, disabled"}
description:
  zh: >
      安全语义的端到端断言：重试只重发没发过的、报告里的尝试次数与注入数为 0、waitfor/waitgone 的时序、被禁用的窗口必须被拒（退出码 4）。
      
  en: >
      End-to-end assertions for the safety semantics: retry repeats only what sent nothing, the report shows attempts and zero injected events, waits behave, and a disabled window is refused with exit code 4.
      
revision: 7a1a124eb7b20fcbebed310d012cbf3926b69a0b
updated_at: "2026-10-03T08:34:07.495Z"
fingerprint: ab0b75ca45752a0a184daf193e8dfd4433f2d690df694dd17c7f347a5e4d374a
source:
  - path: "tests/smoke.ps1"
    line: 167
    end_line: 215
apis:
  - protocol: rpc
    path: "安全语义断言组"
    description:
      zh: >
          重试、报告、等待、闸门拒绝。
          
      en: >
          Retry, report, waits and gate refusals.
          
deps:
  - kind: call
    to: keymouse.cli.main
    from_api: "rpc:安全语义断言组"
    to_api: "rpc:keymouse <命令组>"
    label: {zh: "驱动被测命令", en: "Drive the command"}
---
