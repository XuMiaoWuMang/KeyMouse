---
uid: 3a4b5c6d
id: keymouse.native.user32-enumerate
parent: keymouse.native
tags: [win32]
name: {zh: "窗口枚举与文字", en: "Window enumeration & text"}
description:
  zh: >
      EnumWindows 与两个文字读取（GetWindowText/GetClassName）及其定长缓冲封装。
      
  en: >
      EnumWindows plus the two text reads (GetWindowText/GetClassName) and their fixed-buffer wrappers.
      
revision: 96c306061d63c034a01c2369e4abc295251e2f35
updated_at: "2026-10-03T13:37:44.314Z"
fingerprint: f12933cbfe805c69639883c4ebb1067cc0d0444cd218652f941cddfa7ca49535
source:
  - path: "src/KeyMouse.Core/NativeWindow.cs"
    line: 33
    end_line: 44
  - path: "src/KeyMouse.Core/NativeWindow.cs"
    line: 101
    end_line: 114
apis:
  - protocol: rpc
    path: "user32!EnumWindows"
    description:
      zh: >
          遍历顶层窗口。
          
      en: >
          Walks top-level windows.
          
  - protocol: rpc
    path: "user32!GetWindowText"
    description:
      zh: >
          读窗口标题。
          
      en: >
          Reads a window title.
          
  - protocol: rpc
    path: "user32!GetClassName"
    description:
      zh: >
          读窗口类名。
          
      en: >
          Reads a window class name.
          
  - protocol: rpc
    path: "NativeWindow.GetTitle"
    description:
      zh: >
          标题的安全封装。
          
      en: >
          Safe title wrapper.
          
---
