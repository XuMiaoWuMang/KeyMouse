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
revision: 8d69164cfe5fa0896f595c0bca51f97977ff51b9
updated_at: "2026-10-02T17:50:00Z"
fingerprint: 47db1ca319aa59443427ab4f19ee11a3384ce48bde1eb8c6c1d8e5012b919252
source:
  - path: "NativeWindow.cs"
    line: 1
    end_line: 193
---
