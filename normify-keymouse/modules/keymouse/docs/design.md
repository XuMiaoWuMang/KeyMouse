---
uid: 1c7b22d8
id: keymouse.docs.design
parent: keymouse.docs
tags: [docs]
name: {zh: "设计取舍", en: "Design rationale"}
description:
  zh: >
      为什么是 fail-closed、退出码为何是承诺、闸门查什么、为什么不做“只剩一帧”的启发式检测，以及 v2 的四个决定（单进程、继承只限脚本内、循环必有界、嵌套必须命名）。
  en: >
      Why fail-closed, why the exit code is a promise, what the gate checks, why there is no liveness heuristic - and the four v2 decisions: one process, script-only inheritance, bounded loops, named nesting.
revision: 8d69164cfe5fa0896f595c0bca51f97977ff51b9
updated_at: "2026-10-02T18:20:00Z"
fingerprint: e1608ae262626f0d5f0efe78f90f2eefd036420beca9f01ea2aa4050c1f93af5
source:
  - path: "docs/design.md"
apis:
  - protocol: file
    path: "docs/design.md"
    description:
      zh: >
          设计取舍与已知限制。
      en: >
          Design trade-offs and known limits.
---
