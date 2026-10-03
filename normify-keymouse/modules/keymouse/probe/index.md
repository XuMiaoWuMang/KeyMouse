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
      
revision: 7a1a124eb7b20fcbebed310d012cbf3926b69a0b
updated_at: "2026-10-03T08:34:11.271Z"
fingerprint: b104dda83feba9f56879cb7257c7cd4faa7d2db99e1d0d589c89be55c65a61ae
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
