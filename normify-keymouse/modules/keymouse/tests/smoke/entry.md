---
uid: 182a3c4e
id: keymouse.tests.smoke.entry
parent: keymouse.tests.smoke
tags: [test, desktop]
name: {zh: "入口、编码守卫与靶子启动", en: "Entry, encoding guard & target"}
description:
  zh: >
      冒烟模块 entry：退出码矩阵（未知选择器 3、坏参数 2、未知按键 2、不可解析目标 3）、宿主编码自检，以及断言冒烟靶子确实起来了。靶子本身由 common.ps1 启动——任何模块都可能要驱动它。
      
  en: >
      Smoke module entry: the exit-code matrix (unknown selector 3, bad argument 2, unknown key 2, unresolvable target 3), the host encoding self-check, and the assertion that the smoke target is up. The target itself is started by common.ps1, because any module may want to drive it.
      
revision: bc06440df10935e9e8479ecad7b52098b48df7fd
updated_at: "2026-10-03T12:19:12.569Z"
fingerprint: 6487b857a6c6d2f2dccce5295a7ab3101a195fee67d00faabd9bce5c9a9c8baf
source:
  - path: "tests/smoke/entry.ps1"
apis:
  - protocol: rpc
    path: "pwsh tests/smoke.ps1"
    description:
      zh: >
          跑完桌面冒烟（需要交互式桌面）。
          
      en: >
          Runs the desktop smoke suite; needs an interactive desktop.
          
deps:
  - kind: call
    to: keymouse.tests.smoke-target
    from_api: "rpc:pwsh tests/smoke.ps1"
    to_api: "rpc:KeyMouse.SmokeTarget.exe"
    label: {zh: "启动靶子", en: "Start the target"}
---
