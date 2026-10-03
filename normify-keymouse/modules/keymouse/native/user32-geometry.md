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
      
revision: 6347fbfbc3d7af8839f5dcc9c35501ce2484bdef
updated_at: "2026-10-03T11:11:56.203Z"
fingerprint: d39f953f3e29de7157808217fe297fca70c2373b4379b06e762d25bbb047f540
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
