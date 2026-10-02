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
      
revision: 8d69164cfe5fa0896f595c0bca51f97977ff51b9
updated_at: "2026-10-02T17:38:44.180Z"
fingerprint: 6b9df14b5b002ca1ed3e5a658c4f07243664c2bb41a65af6d9faeb0d4d1f0902
source:
  - path: "tests/smoke.ps1"
    line: 1
    end_line: 343
---
