---
uid: 1c7b22d8
id: keymouse.docs.design
parent: keymouse.docs
tags: [docs, ocr, design]
name: {zh: "设计取舍", en: "Design rationale"}
description:
  zh: >
      为什么是 fail-closed、退出码为何是承诺、闸门查什么、为什么不做“只剩一帧”的启发式检测，v2 的四个决定（单进程、继承只限脚本内、循环必有界、嵌套必须命名），以及**感知与控制流（识图 / OCR）的完整设计**：三条硬边界、实测一致性数据、probe 的 JSON 形状、共识判定、引擎适配器与退出码 6。
      
  en: >
      Why fail-closed, why the exit code is a promise, what the gate checks, why there is no liveness heuristic, the four v2 decisions, and the full design for perception and control flow (OCR): three hard boundaries, measured consistency numbers, the probe JSON shape, consensus reading, engine adapters and exit code 6.
      
revision: 20f54999ca6d8eb01052e467d5bc9ed27b04b6c0
updated_at: "2026-10-03T06:00:56.959Z"
fingerprint: c3604f16969c4e9010123ceb5fe8fbf79f88fe3a99ef77aad960680bb484612d
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
