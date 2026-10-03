---
uid: 8f9a0b1c
id: keymouse.native.dwm
parent: keymouse.native
tags: [win32, dwm]
name: {zh: "DWM 遮盖查询", en: "DWM cloaking"}
description:
  zh: >
      DwmGetWindowAttribute(DWMWA_CLOAKED)：识别挂起的 UWP 应用与属于其他虚拟桌面的窗口——这类窗口“可见”但永远收不到输入。
      
  en: >
      DwmGetWindowAttribute(DWMWA_CLOAKED) spots suspended UWP apps and windows on another virtual desktop: they look visible yet can never receive input.
      
revision: 96c306061d63c034a01c2369e4abc295251e2f35
updated_at: "2026-10-03T13:37:44.313Z"
fingerprint: f12933cbfe805c69639883c4ebb1067cc0d0444cd218652f941cddfa7ca49535
source:
  - path: "src/KeyMouse.Core/NativeWindow.cs"
    line: 88
    end_line: 89
  - path: "src/KeyMouse.Core/NativeWindow.cs"
    line: 173
    end_line: 185
apis:
  - protocol: rpc
    path: "dwmapi!DwmGetWindowAttribute"
    description:
      zh: >
          查询 DWM 窗口属性。
          
      en: >
          Queries a DWM window attribute.
          
  - protocol: rpc
    path: "NativeWindow.IsCloaked"
    description:
      zh: >
          该窗口是否被 DWM 遮盖。
          
      en: >
          Whether DWM considers it cloaked.
          
---
