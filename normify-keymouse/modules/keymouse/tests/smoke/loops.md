---
uid: 7a1f1102
id: keymouse.tests.smoke.loops
parent: keymouse.tests.smoke
tags: [test, desktop]
name: {zh: "脚本循环冒烟", en: "Script loop smoke"}
description:
  zh: >
      文本脚本那边的循环与"一个进程跑到底"：`repeat 3` 的第一次与最后一次真的打进去了、次数到了就停、缺 `end` 或多余的 `end` 在跑之前就被拒绝。与流程格式的循环（flowloops 模块）刻意分开，两者是两套东西。
      
  en: >
      Loops in the text-script syntax, plus one process from start to finish: the first and last of a repeat 3 really land, the loop stops at the requested count, and a missing or stray end is refused before anything runs. Deliberately separate from loops in the flow format (the flowloops module): they are two different things.
      
revision: bc06440df10935e9e8479ecad7b52098b48df7fd
updated_at: "2026-10-03T12:19:12.568Z"
fingerprint: b859d35dc933df385974368f86902452f294676ace6536a920eeb8423b2b04a2
source:
  - path: "tests/smoke/loops.ps1"
apis:
  - protocol: rpc
    path: "脚本循环断言组"
    description:
      zh: >
          重复执行、边界与提前拒绝。
          
      en: >
          Repetition, boundaries and early refusal.
          
---
