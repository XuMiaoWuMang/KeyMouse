---
uid: 0d1e2f3a
id: keymouse.window.gate
parent: keymouse.window
tags: [window, safety]
name: {zh: "资格闸门", en: "Eligibility gate"}
description:
  zh: >
      注入前的全部客观判定：隐藏拒绝、最小化拒绝（除非 --allow-restore）、DWM 遮盖拒绝、被禁用拒绝（WS_DISABLED 会吞掉输入）、WM_NULL 无响应拒绝。判定结论带问题清单与备注，供 window inspect 展示。
      
  en: >
      Every objective check before injection: refuse hidden, minimized (unless --allow-restore), DWM-cloaked, disabled windows, and windows that fail the WM_NULL round trip. The verdict carries problem and note lists for `window inspect`.
      
revision: d870b0c5dfd7fe29675863c0288b572a6111a8d4
updated_at: "2026-10-03T07:28:04.467Z"
fingerprint: f12cc44733c8cfcbd93bebdd0a2bce3405d6d3cd24d397e1160a51a364188154
source:
  - path: "WindowEligibility.cs"
    line: 1
    end_line: 73
apis:
  - protocol: rpc
    path: "WindowEligibility.Check"
    description:
      zh: >
          给出可用性结论及理由。
          
      en: >
          Returns the usability verdict with reasons.
          
deps:
  - kind: call
    to: keymouse.window.snapshot
    from_api: "rpc:WindowEligibility.Check"
    to_api: "rpc:WindowInfo.Capture"
    label: {zh: "读状态", en: "Read state"}
  - kind: call
    to: keymouse.native.dwm
    from_api: "rpc:WindowEligibility.Check"
    to_api: "rpc:NativeWindow.IsCloaked"
    label: {zh: "遮盖检查", en: "Cloak check"}
  - kind: call
    to: keymouse.native.window-probe
    from_api: "rpc:WindowEligibility.Check"
    to_api: "rpc:NativeWindow.ResponseMs"
    label: {zh: "响应探测", en: "Probe response"}
  - kind: call
    to: keymouse.native.user32-layer
    from_api: "rpc:WindowEligibility.Check"
    to_api: "rpc:user32!ShowWindow"
    label: {zh: "还原窗口", en: "Restore window"}
---
