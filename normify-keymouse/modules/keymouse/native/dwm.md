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
      
revision: 6347fbfbc3d7af8839f5dcc9c35501ce2484bdef
updated_at: "2026-10-03T11:11:56.203Z"
fingerprint: d39f953f3e29de7157808217fe297fca70c2373b4379b06e762d25bbb047f540
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
