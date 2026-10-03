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
      
revision: d870b0c5dfd7fe29675863c0288b572a6111a8d4
updated_at: "2026-10-03T07:28:04.453Z"
fingerprint: 0b87df5cca1badf5983b75c5f73c140e8c388819cd6404f29cf2eef6978e4f42
source:
  - path: "NativeWindow.cs"
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
