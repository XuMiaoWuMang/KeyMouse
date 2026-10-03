---
uid: 6d7e8f9a
id: keymouse.native.user32-focus
parent: keymouse.native
tags: [win32, focus]
name: {zh: "前台窗口控制", en: "Foreground control"}
description:
  zh: >
      读取当前前台窗口、请求把它换成目标（SetForegroundWindow/BringWindowToTop），以及取根窗口/属主/窗口线程进程号——聚焦验证的全部依据。
      
  en: >
      Reads the current foreground window, asks for the target to become it, and resolves root/owner/thread-pid - the whole basis of focus verification.
      
revision: e03d53e4c41a8a7220e83123d7f8a44a54ff8dab
updated_at: "2026-10-03T08:15:19.861Z"
fingerprint: 0b87df5cca1badf5983b75c5f73c140e8c388819cd6404f29cf2eef6978e4f42
source:
  - path: "NativeWindow.cs"
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
