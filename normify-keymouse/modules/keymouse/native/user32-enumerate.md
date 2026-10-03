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
      
revision: d870b0c5dfd7fe29675863c0288b572a6111a8d4
updated_at: "2026-10-03T07:28:04.453Z"
fingerprint: 0b87df5cca1badf5983b75c5f73c140e8c388819cd6404f29cf2eef6978e4f42
source:
  - path: "NativeWindow.cs"
    line: 33
    end_line: 44
  - path: "NativeWindow.cs"
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
