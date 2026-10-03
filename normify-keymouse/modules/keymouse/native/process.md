---
uid: 9a0b1c2d
id: keymouse.native.process
parent: keymouse.native
tags: [win32, perf]
name: {zh: "进程名查询与缓存", en: "Process name & cache"}
description:
  zh: >
      QueryFullProcessImageName 一次系统调用取进程名（相比 Process.GetProcessById 快三十多倍），并按 pid 缓存整个进程生命周期——一条命令枚举到的同一进程只查一次。
      
  en: >
      QueryFullProcessImageName resolves a process name in one syscall - over thirty times faster than Process.GetProcessById - and the answer is cached per pid for the life of the run.
      
revision: 96c306061d63c034a01c2369e4abc295251e2f35
updated_at: "2026-10-03T13:37:44.314Z"
fingerprint: f12933cbfe805c69639883c4ebb1067cc0d0444cd218652f941cddfa7ca49535
source:
  - path: "src/KeyMouse.Core/NativeWindow.cs"
    line: 125
    end_line: 171
apis:
  - protocol: rpc
    path: "NativeWindow.ProcessNameOf"
    description:
      zh: >
          pid → 进程名（带缓存）。
          
      en: >
          Pid to process name, cached.
          
  - protocol: rpc
    path: "kernel32!OpenProcess"
    description:
      zh: >
          以查询权限打开进程。
          
      en: >
          Opens a process for limited query.
          
  - protocol: rpc
    path: "kernel32!QueryFullProcessImageName"
    description:
      zh: >
          取进程映像路径。
          
      en: >
          Reads the process image path.
          
---
