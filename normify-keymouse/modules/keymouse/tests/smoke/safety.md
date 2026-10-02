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
revision: 8d69164cfe5fa0896f595c0bca51f97977ff51b9
updated_at: "2026-10-02T18:40:00Z"
fingerprint: 6b9df14b5b002ca1ed3e5a658c4f07243664c2bb41a65af6d9faeb0d4d1f0902
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
