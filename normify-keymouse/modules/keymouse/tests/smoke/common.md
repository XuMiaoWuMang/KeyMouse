---
uid: 7a1f1101
id: keymouse.tests.smoke.common
parent: keymouse.tests.smoke
tags: [test, desktop]
name: {zh: "冒烟公共部分", en: "Smoke common"}
description:
  zh: >
      模块共享的准备与工具：`Check`、`$Exe`/`$target`、宿主编码自检（中文输出解码不对就立刻停，否则后面每条断言都会以假失败收场）、剪贴板保存，以及**启动冒烟靶子**——它是基建而不是某个模块的职责，所以放在这里，任何模块单独跑都不会缺靶子。
      
  en: >
      What every module shares: Check, $Exe/$target, the host encoding self-check (a host that decodes Chinese wrongly fails everything downstream for the wrong reason), clipboard saving, and starting the smoke target - infrastructure rather than one module duty, so any module can run on its own.
      
revision: bc06440df10935e9e8479ecad7b52098b48df7fd
updated_at: "2026-10-03T12:19:12.567Z"
fingerprint: d093bcd7d530ca0528e77c12c5da64c1514b571fb6ae9e5ef8c708c270844491
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
