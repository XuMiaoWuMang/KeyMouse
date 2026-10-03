---
uid: a7c41e90
id: keymouse.native.capture
parent: keymouse.native
tags: [win32, gdi, perception]
name: {zh: "窗口内容采集", en: "Window content capture"}
description:
  zh: >
      PrintWindow + GDI DIB 区块：把窗口（含后台窗口）渲染成像素，不画文本光标、不需要前台。同文件的屏幕抓取兄弟函数是已验证的默认路径，因为 PrintWindow 在本机几何对不齐。
      
  en: >
      PrintWindow plus a GDI DIB section: renders a window (including background windows) into pixels, drawing no text caret and needing no foreground. Its screen-grab sibling is the verified default, because PrintWindow's geometry does not line up on this system.
      
revision: d870b0c5dfd7fe29675863c0288b572a6111a8d4
updated_at: "2026-10-03T07:31:02.526Z"
fingerprint: 059722e432c45e0fa57ce96dd33212062198928eadf36b7fed06d5a4b2415ff1
source:
  - path: "NativeCapture.cs"
    line: 1
    end_line: 177
apis:
  - protocol: rpc
    path: "NativeCapture.TryCapture"
    description:
      zh: >
          把一个窗口通过 PrintWindow 渲染成自上而下的 32 位像素。
          
      en: >
          Renders a window through PrintWindow into a top-down 32-bit buffer.
          
  - protocol: rpc
    path: "NativeCapture.TryCaptureScreen"
    description:
      zh: >
          直接从屏幕拷贝一块矩形——已被验证能读回文字的取像路径。
          
      en: >
          Copies a screen rectangle straight from the display - the capture path verified to read text back.
          
---
