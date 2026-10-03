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
      
revision: 96c306061d63c034a01c2369e4abc295251e2f35
updated_at: "2026-10-03T13:37:44.313Z"
fingerprint: f12933cbfe805c69639883c4ebb1067cc0d0444cd218652f941cddfa7ca49535
source:
  - path: "src/KeyMouse.Core/NativeWindow.cs"
    line: 1
    end_line: 193
---
