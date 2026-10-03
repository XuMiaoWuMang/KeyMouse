---
uid: 7e8f9a0b
id: keymouse.native.user32-layer
parent: keymouse.native
tags: [win32]
name: {zh: "层级、命中与显示", en: "Layering, hit test & show"}
description:
  zh: >
      Z 序/属主关系（GetWindow）、屏幕点命中（WindowFromPoint）、以及最小化窗口的显式还原（ShowWindow）——还原只会在 --allow-restore 下发生。
      
  en: >
      Owner and Z-order relations, screen-point hit testing, and explicit restore of a minimized window - a restore that only happens under --allow-restore.
      
revision: 96c306061d63c034a01c2369e4abc295251e2f35
updated_at: "2026-10-03T13:37:44.314Z"
fingerprint: f12933cbfe805c69639883c4ebb1067cc0d0444cd218652f941cddfa7ca49535
source:
  - path: "src/KeyMouse.Core/NativeWindow.cs"
    line: 90
    end_line: 99
  - path: "src/KeyMouse.Core/NativeWindow.cs"
    line: 115
    end_line: 123
apis:
  - protocol: rpc
    path: "user32!GetWindow"
    description:
      zh: >
          按 GW_OWNER 等关系取窗口。
          
      en: >
          Gets a related window such as the owner.
          
  - protocol: rpc
    path: "user32!WindowFromPoint"
    description:
      zh: >
          屏幕点下的窗口。
          
      en: >
          Window under a screen point.
          
  - protocol: rpc
    path: "user32!ShowWindow"
    description:
      zh: >
          显示/还原窗口。
          
      en: >
          Shows or restores a window.
          
  - protocol: rpc
    path: "NativeWindow.Root"
    description:
      zh: >
          归一到根窗口。
          
      en: >
          Normalises to the root window.
          
  - protocol: rpc
    path: "NativeWindow.GetOwner"
    description:
      zh: >
          取属主（无主=0）。
          
      en: >
          Owner window, zero when unowned.
          
---
