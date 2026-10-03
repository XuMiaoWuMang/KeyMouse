---
uid: 6d7e8f9a
id: keymouse.native.user32-focus
parent: keymouse.native
tags: [win32, focus]
name: {zh: "前台窗口控制", en: "Foreground control"}
description:
  zh: >
      前台相关的 P/Invoke：GetForegroundWindow / SetForegroundWindow / BringWindowToTop / GetWindowThreadProcessId / AttachThreadInput / GetCurrentThreadId。后三个是"前台锁"的官方绕法：把调用者的输入队列接到当前前台线程上，`SetForegroundWindow` 才会被接受。
      
  en: >
      The foreground P/Invokes: GetForegroundWindow, SetForegroundWindow, BringWindowToTop, GetWindowThreadProcessId, AttachThreadInput and GetCurrentThreadId. The last three are the documented way around the foreground lock: join the caller input queue to the foreground thread and SetForegroundWindow is accepted.
      
revision: 96c306061d63c034a01c2369e4abc295251e2f35
updated_at: "2026-10-03T13:37:48.970Z"
fingerprint: f12933cbfe805c69639883c4ebb1067cc0d0444cd218652f941cddfa7ca49535
source:
  - path: "src/KeyMouse.Core/NativeWindow.cs"
    line: 69
    end_line: 83
apis:
  - protocol: rpc
    path: "user32!GetForegroundWindow"
    description:
      zh: >
          当前前台窗口。
          
      en: >
          The current foreground window.
          
  - protocol: rpc
    path: "user32!SetForegroundWindow"
    description:
      zh: >
          请求切换前台。
          
      en: >
          Asks to change the foreground window.
          
  - protocol: rpc
    path: "user32!BringWindowToTop"
    description:
      zh: >
          提到 Z 序顶部。
          
      en: >
          Raises the window in Z order.
          
  - protocol: rpc
    path: "user32!GetAncestor"
    description:
      zh: >
          取根窗口/属主链。
          
      en: >
          Resolves root and owner chain.
          
  - protocol: rpc
    path: "user32!GetWindowThreadProcessId"
    description:
      zh: >
          窗口→进程号。
          
      en: >
          Window to process id.
          
---
