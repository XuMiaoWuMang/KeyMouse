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
revision: 8d69164cfe5fa0896f595c0bca51f97977ff51b9
updated_at: "2026-10-02T18:00:00Z"
fingerprint: 47db1ca319aa59443427ab4f19ee11a3384ce48bde1eb8c6c1d8e5012b919252
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
