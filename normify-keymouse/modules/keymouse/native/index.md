---
uid: 5e6f7081
id: keymouse.native
parent: keymouse
tags: [win32, pinvoke]
name: {zh: "原生互操作", en: "Native interop"}
description:
  zh: >
      全部 P/Invoke 声明与其薄封装：user32 输入/窗口 API、DWM 遮盖查询、WM_NULL 响应探测、进程名查询（QueryFullProcessImageName + pid 缓存）。这一层只管「怎么调」，不含业务判断。
      
  en: >
      Every P/Invoke declaration and its thin wrapper: user32 input/window APIs, DWM cloaking, the WM_NULL responsiveness probe, and process names via QueryFullProcessImageName with a pid cache.
      
revision: 6347fbfbc3d7af8839f5dcc9c35501ce2484bdef
updated_at: "2026-10-03T11:11:56.203Z"
fingerprint: d39f953f3e29de7157808217fe297fca70c2373b4379b06e762d25bbb047f540
source:
  - path: "src/KeyMouse.Core/NativeWindow.cs"
    line: 1
    end_line: 193
---
