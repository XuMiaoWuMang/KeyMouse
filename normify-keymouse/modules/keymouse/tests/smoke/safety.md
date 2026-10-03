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
      
revision: e03d53e4c41a8a7220e83123d7f8a44a54ff8dab
updated_at: "2026-10-03T08:15:19.890Z"
fingerprint: 2b0bcc62780e30e68665cfccee66576416b0b8c5c5aca54381a6d00561dc8637
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
