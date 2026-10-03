---
uid: cf3d4e5a
id: keymouse.cli.window.targeting
parent: keymouse.cli.window
tags: [window, focus, safety]
name: {zh: "目标准备与聚焦策略", en: "Target preparation & focus policy"}
description:
  zh: >
      每条带选择器的命令都要过的公共前段：解析候选 → 把用户降到客观判定（无匹配/不可用/歧义各有不同报错）→ 按 gentle 或 none 策略抢前台 → 再次回读确认窗口仍可用。任何一步不过就抛 CommandFailure，注入之前终止。
      
  en: >
      The shared prologue of every targeted command: resolve candidates, turn user input into an objective verdict, take the foreground per the gentle/none policy, then re-read the window to confirm it is still usable. Any failure throws before injection.
      
revision: 01b8c3c91b12f7561f62aa9f6af36e7eb9e3bb94
updated_at: "2026-10-03T10:33:15.215Z"
fingerprint: bbcd909665ff033ce01c238b5437796aded55168ead822ef453ff228abd0fee5
source:
  - path: "Program.cs"
    line: 61
    end_line: 160
apis:
  - protocol: rpc
    path: "PrepareTarget"
    description:
      zh: >
          解析并验证目标（含闸门）。
          
      en: >
          Resolves and verifies the target.
          
  - protocol: rpc
    path: "FocusTarget"
    description:
      zh: >
          按策略聚焦并回读。
          
      en: >
          Focuses per policy and reads back.
          
  - protocol: rpc
    path: "PreferCandidates"
    description:
      zh: >
          可见优先、无主优先的收敛。
          
      en: >
          Prefers visible, then unowned.
          
deps:
  - kind: call
    to: keymouse.window.resolve
    from_api: "rpc:PrepareTarget"
    to_api: "rpc:WindowResolver.Resolve"
    label: {zh: "解析编排与闸门", en: "Resolve & gate"}
  - kind: call
    to: keymouse.cli.failure
    from_api: "rpc:PrepareTarget"
    to_api: "rpc:CommandFailure"
    label: {zh: "不合格即终止", en: "Refuse"}
---
