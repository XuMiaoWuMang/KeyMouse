---
uid: 4b5c6d7e
id: keymouse.native.user32-state
parent: keymouse.native
tags: [win32]
name: {zh: "窗口状态查询", en: "Window state queries"}
description:
  zh: >
      四个布尔状态：可见、最小化、被禁用（WS_DISABLED）、是否仍是有效窗口。闸门的一半判据来自这里。
      
  en: >
      Four boolean facts: visible, minimized, disabled (WS_DISABLED) and still-valid; half of the gate's evidence comes from here.
      
revision: 96c306061d63c034a01c2369e4abc295251e2f35
updated_at: "2026-10-03T13:37:44.314Z"
fingerprint: f12933cbfe805c69639883c4ebb1067cc0d0444cd218652f941cddfa7ca49535
source:
  - path: "src/KeyMouse.Core/NativeWindow.cs"
    line: 45
    end_line: 56
apis:
  - protocol: rpc
    path: "user32!IsWindowVisible"
    description:
      zh: >
          是否可见。
          
      en: >
          Whether the window is visible.
          
  - protocol: rpc
    path: "user32!IsIconic"
    description:
      zh: >
          是否最小化。
          
      en: >
          Whether the window is minimized.
          
  - protocol: rpc
    path: "user32!IsWindowEnabled"
    description:
      zh: >
          是否启用（禁用会吞掉输入）。
          
      en: >
          Whether it is enabled; disabled windows swallow input.
          
  - protocol: rpc
    path: "user32!IsWindow"
    description:
      zh: >
          句柄是否仍是有效窗口。
          
      en: >
          Whether the handle is still a valid window.
          
---
