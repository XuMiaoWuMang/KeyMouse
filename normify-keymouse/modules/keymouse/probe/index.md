---
uid: b8d52fa1
id: keymouse.probe
parent: keymouse
tags: [perception, ocr]
name: {zh: "感知出口", en: "Perception exit"}
description:
  zh: >
      `probe` 命令：把一块客户区区域读成结构化文本并诚实报告可信度。三条硬边界在这里汇合——只报告不判断、读不到就是退出码 6、读到空是事实而不是失败。
      
  en: >
      The `probe` command: reads a client-relative region into structured text and honestly reports how trustworthy the read is. Three hard boundaries meet here - it reports and never judges, an unreadable region exits 6, and an empty region is a fact rather than a failure.
      
revision: d870b0c5dfd7fe29675863c0288b572a6111a8d4
updated_at: "2026-10-03T07:31:02.526Z"
fingerprint: c8a37194838b7eb26afe170b7729b9947ba9a014cdcb0881b2b4e87d9c1aa6aa
source:
  - path: "Probe.cs"
    line: 45
    end_line: 222
---
