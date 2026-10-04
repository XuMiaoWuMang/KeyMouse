---
uid: 7d2f8d9e
id: keymouse.tests.smoke
parent: keymouse.tests
tags: [test, desktop]
name: {zh: "桌面冒烟", en: "Desktop smoke"}
description:
  zh: >
      冒烟运行器：`-Only 模块[,…]` 只跑指定模块（平时用这个）、`-List` 列出来、不给参数就是全部（= 总测试的一部分）。它负责载入 common、按顺序 dot-source 选中的模块、最后收尾。收尾**只收自己起的东西**（靶子、剪贴板）：以前它会杀掉所有 KeyMouse 进程，那等于把用户编辑器起的常驻 Runner 也杀了——既粗鲁，又会让下一次冒烟以假失败收场。
      
  en: >
      The smoke runner: -Only module[,...] runs just those (the everyday case), -List lists them, and no arguments runs everything (part of the full test). It loads common, dot-sources the selected modules in order, then tears down. Teardown only touches what it started (the target, the clipboard): it used to kill every KeyMouse process, which meant killing the resident Runner the user editor had started - rude, and a way to make the next smoke run fail for no real reason.
      
revision: ef3cf740e80bd62dd7540df6ee4c526380163233
updated_at: "2026-10-04T14:23:38.097Z"
fingerprint: 70bf6c42c4d4b72ce1bee77d04454adcd8261ff0030847cadee057958865cb62
source:
  - path: "tests/smoke.ps1"
---
