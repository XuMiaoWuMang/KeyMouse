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
      
revision: d870b0c5dfd7fe29675863c0288b572a6111a8d4
updated_at: "2026-10-03T07:28:04.454Z"
fingerprint: 0b87df5cca1badf5983b75c5f73c140e8c388819cd6404f29cf2eef6978e4f42
source:
  - path: "NativeWindow.cs"
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
