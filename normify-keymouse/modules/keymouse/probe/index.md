---
uid: b8d52fa1
id: keymouse.probe
parent: keymouse
tags: [perception, ocr]
name: {zh: "感知出口", en: "Perception exit"}
description:
  zh: >
      `probe` 命令：把一块区域读成结构化文本并诚实报告可信度。三条硬边界在这里汇合——只报告不判断、读不到就是退出码 6、读到空是事实而不是失败。默认把抓到的**原始像素**交给引擎（放大与归一化都是选项），读到的文字原样回报，两次读取必须逐字节一致。
      
  en: >
      The `probe` command: reads a region into structured text and honestly reports how trustworthy the read is. Three hard boundaries meet here - it reports and never judges, an unreadable region exits 6, and an empty region is a fact rather than a failure. The captured pixels go to the engine unchanged by default (upscaling and normalisation are options), and the text comes back verbatim: two reads must agree byte for byte.
      
revision: 1b316650150f1540368ee548f7d9992ccaa3239e
updated_at: "2026-10-03T11:27:19.953Z"
fingerprint: a2eb516cd040a7a60ad8ddefbc45604cb153a269959d6bfb4c10e52481e8655d
source:
  - path: "src/KeyMouse.Core/Probe.cs"
    line: 45
    end_line: 240
deps:
  - kind: call
    to: keymouse.pick.overlay
    to_api: "rpc:RegionPicker.Pick"
    label: {zh: "--pick-region：先让人框一块", en: "Pick a region first"}
  - kind: call
    to: keymouse.pick.placement
    to_api: "rpc:RegionCommand.Describe"
    label: {zh: "把选区翻成坐标系", en: "Turn the pick into a space"}
---
