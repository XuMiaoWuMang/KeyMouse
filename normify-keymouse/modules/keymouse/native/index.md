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
      
revision: e03d53e4c41a8a7220e83123d7f8a44a54ff8dab
updated_at: "2026-10-03T08:15:19.860Z"
fingerprint: 0b87df5cca1badf5983b75c5f73c140e8c388819cd6404f29cf2eef6978e4f42
source:
  - path: "NativeWindow.cs"
    line: 1
    end_line: 193
---
