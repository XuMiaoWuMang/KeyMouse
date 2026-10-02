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
revision: 8d69164cfe5fa0896f595c0bca51f97977ff51b9
updated_at: "2026-10-02T18:00:00Z"
fingerprint: 47db1ca319aa59443427ab4f19ee11a3384ce48bde1eb8c6c1d8e5012b919252
source:
  - path: "NativeWindow.cs"
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
