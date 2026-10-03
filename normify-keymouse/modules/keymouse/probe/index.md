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
      
revision: e03d53e4c41a8a7220e83123d7f8a44a54ff8dab
updated_at: "2026-10-03T08:15:51.237Z"
fingerprint: 55ffec1c5fe76cdac87f8fdb9317271575c268abf8197580f32d864301166b94
source:
  - path: "Probe.cs"
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
