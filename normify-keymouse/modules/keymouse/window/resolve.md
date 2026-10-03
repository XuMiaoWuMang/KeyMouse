---
uid: 2f3a4b5c
id: keymouse.window.resolve
parent: keymouse.window
tags: [window]
name: {zh: "解析编排", en: "Resolution orchestration"}
description:
  zh: >
      一次解析的完整结果：桌面全部窗口、选择器匹配者、参与收敛的候选、闸门通过者与逐个被拒的理由。一次枚举供给后续所有判定与错误文案使用。
      
  en: >
      The full result of one resolution: every window, the matches, the candidates that took part, the survivors, and why the rest were rejected. One enumeration feeds every later check and message.
      
revision: d870b0c5dfd7fe29675863c0288b572a6111a8d4
updated_at: "2026-10-03T07:28:04.468Z"
fingerprint: cb836896284ad7465ff444e852b54fea542faf0070408a40a1f7df4966fb2bee
source:
  - path: "WindowResolver.cs"
    line: 1
    end_line: 45
apis:
  - protocol: rpc
    path: "WindowResolver.Resolve"
    description:
      zh: >
          解析并判定一个选择器。
          
      en: >
          Resolves and judges one selector.
          
deps:
  - kind: call
    to: keymouse.window.select.find
    from_api: "rpc:WindowResolver.Resolve"
    to_api: "rpc:WindowLocator.Find"
    label: {zh: "先匹配", en: "Match first"}
  - kind: call
    to: keymouse.window.gate
    from_api: "rpc:WindowResolver.Resolve"
    to_api: "rpc:WindowEligibility.Check"
    label: {zh: "再逐个过闸门", en: "Then gate each"}
---
