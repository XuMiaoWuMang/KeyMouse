---
uid: 7d2f8d9e
id: keymouse.tests.smoke
parent: keymouse.tests
tags: [test, desktop]
name: {zh: "桌面冒烟", en: "Desktop smoke"}
description:
  zh: >
      唯一能证明“手没抖”的那关：启动自己的靶子窗口，跑 50+ 项断言（退出码矩阵、打字往返比对、拖拽选中、变量、重试与报告、等待、禁用窗口、--dry-run 零输入、vk: 逃生口、stdin、目标继承、循环、运行期间恒为 1 个进程）。
      
  en: >
      The only gate that proves nothing was fumbled: it starts its own target window and runs 50+ checks - exit codes, typed-text round trip, drag selection, variables, retry and report, waits, a disabled window, dry-run, the vk: hatch, stdin, inheritance, loops, and exactly one process throughout.
      
revision: 7a1a124eb7b20fcbebed310d012cbf3926b69a0b
updated_at: "2026-10-03T08:34:07.494Z"
fingerprint: ab0b75ca45752a0a184daf193e8dfd4433f2d690df694dd17c7f347a5e4d374a
source:
  - path: "tests/smoke.ps1"
    line: 1
    end_line: 343
---
