---
uid: 5c6d7e8f
id: keymouse.native.user32-geometry
parent: keymouse.native
tags: [win32, coordinates]
name: {zh: "矩形与坐标映射", en: "Rects & coordinate mapping"}
description:
  zh: >
      窗口矩形（含装饰）、客户区矩形，以及客户区↔屏幕的双向映射。客户区坐标能不能落到正确的像素，全靠这四个。
      
  en: >
      The window rect (chrome included), the client rect, and both directions of client-to-screen mapping; correct client-area math rests on these four.
      
revision: 96c306061d63c034a01c2369e4abc295251e2f35
updated_at: "2026-10-03T13:37:44.314Z"
fingerprint: f12933cbfe805c69639883c4ebb1067cc0d0444cd218652f941cddfa7ca49535
source:
  - path: "src/KeyMouse.Core/NativeWindow.cs"
    line: 57
    end_line: 68
apis:
  - protocol: rpc
    path: "user32!GetWindowRect"
    description:
      zh: >
          整个窗口的屏幕矩形。
          
      en: >
          Screen rect of the whole window.
          
  - protocol: rpc
    path: "user32!GetClientRect"
    description:
      zh: >
          客户区矩形。
          
      en: >
          Client-area rect.
          
  - protocol: rpc
    path: "user32!ClientToScreen"
    description:
      zh: >
          客户区坐标→屏幕。
          
      en: >
          Client to screen.
          
  - protocol: rpc
    path: "user32!ScreenToClient"
    description:
      zh: >
          屏幕→客户区坐标。
          
      en: >
          Screen to client.
          
---
