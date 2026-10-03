---
uid: 7a1f0f03
id: keymouse.tests.smoke.act
parent: keymouse.tests.smoke
tags: [test, desktop, perception]
name: {zh: "找字并点它冒烟", en: "Find-and-act smoke"}
description:
  zh: >
      真桌面上的那一段：往靶子打字 → `probe --find` 给出框与客户区点击点 → 屏幕上没有的字退出码 3 → `click-text` 真的点到了它看到的字（点完再 ctrl+a/ctrl+c，剪贴板里回读得到原文）→ `when` 前提成立就执行、不成立按 `else: skip` 跳过、按 `else: fail` 退出码 3。
      
  en: >
      The desktop half: type into the target, have `probe --find` report the box and the client click point, get exit code 3 for text that is not there, watch `click-text` actually click what it saw (then ctrl+a/ctrl+c and read the text back off the clipboard), and see a `when` precondition run its step, skip it, or fail with exit 3 depending on `else`.
      
revision: bc06440df10935e9e8479ecad7b52098b48df7fd
updated_at: "2026-10-03T12:19:12.570Z"
fingerprint: 345ebe387c68d4c07cde9018ab3c533e2862f9a64e32c9f3654db8b450909d71
source:
  - path: "tests/smoke/act.ps1"
apis:
  - protocol: rpc
    path: "找字与前置条件断言组"
    description:
      zh: >
          --find、click-text、when 三条路。
          
      en: >
          The --find, click-text and when paths.
          
deps:
  - kind: call
    to: keymouse.probe.engine
    to_api: "rpc:Probe.ReadOnce"
    label: {zh: "读区域", en: "Read the region"}
  - kind: call
    to: keymouse.flow.runner
    to_api: "rpc:FlowRunner.Run"
    label: {zh: "执行流程", en: "Run the flow"}
---
