---
uid: 7d2f8d9e
id: keymouse.tests.smoke
parent: keymouse.tests
tags: [test, desktop]
name: {zh: "桌面冒烟", en: "Desktop smoke"}
description:
  zh: >
      冒烟运行器：`-Only 模块[,…]` 只跑指定模块（平时用这个）、`-List` 列出来、不给参数就是全部（= 总测试的一部分）。它负责载入 common、按顺序 dot-source 选中的模块、最后收尾（停靶子、还原剪贴板）并给出通过/失败计数。
      
  en: >
      The smoke runner: `-Only module[,...]` runs just those (the everyday case), `-List` lists them, and no arguments runs everything (part of the full test). It loads common, dot-sources the selected modules in order, then tears down (stops the target, restores the clipboard) and reports the pass/fail counts.
      
revision: 1ebae14ff2430b597cc4a1695a71ddf788879db1
updated_at: "2026-10-03T12:49:24.518Z"
fingerprint: d21cd254e94a93807f9c3e824978a6833e6d4334d1fa261543a004090aebdfac
source:
  - path: "tests/smoke.ps1"
---
