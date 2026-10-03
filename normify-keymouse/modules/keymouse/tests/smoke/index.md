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
      
revision: 1b316650150f1540368ee548f7d9992ccaa3239e
updated_at: "2026-10-03T11:27:19.955Z"
fingerprint: 2ab5b1e29a47f02cf1bf9588824895c65511b838f298921b03517b68cf0c2879
source:
  - path: "tests/smoke.ps1"
    line: 1
    end_line: 343
---
