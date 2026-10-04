---
uid: 7a1f1101
id: keymouse.tests.smoke.common
parent: keymouse.tests.smoke
tags: [test, desktop]
name: {zh: "冒烟公共部分", en: "Smoke common"}
description:
  zh: >
      模块共享的准备与工具：`Check`、`$Exe`/`$target`、宿主编码自检、剪贴板保存、**启动冒烟靶子**，以及 `Wait-ForWindow`——等某个窗口出现（可选再等一小会儿让它装好鼠标捕获）而不是睡固定时长。固定睡眠在"起来需要多久取决于抢前台快不快"的覆盖层上最不可靠：聚焦改成先接线程输入队列后，1500ms 从"稳"变成"偶尔早到"，区域拾取的两条断言因此挂了。
      
  en: >
      What every module shares: Check, $Exe/$target, the host encoding self-check, clipboard saving, starting the smoke target, and Wait-ForWindow - wait until a window exists (optionally plus a moment to arm) instead of sleeping a fixed time. A fixed sleep is worst exactly where startup depends on how fast the foreground is won: once focus attached the thread input queue first, 1500ms went from reliable to occasionally early.
      
revision: ef3cf740e80bd62dd7540df6ee4c526380163233
updated_at: "2026-10-04T14:23:38.097Z"
fingerprint: d6e346a5b8723702f7d6daf705388979468e9fdb5539eb8b43fdae8a607b9f9a
source:
  - path: "tests/smoke/common.ps1"
apis:
  - protocol: rpc
    path: "Check"
    description:
      zh: >
          记一条断言结果（ok/FAIL），并累计计数。
          
      en: >
          Records one assertion (ok/FAIL) and keeps the counts.
          
---
