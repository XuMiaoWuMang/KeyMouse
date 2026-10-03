---
uid: 7a1f0c42
id: keymouse.tests.evidence
parent: keymouse.tests
tags: [test, desktop, evidence]
name: {zh: "测量留档", en: "Evidence archive"}
description:
  zh: >
      把一次测量变成可回看的证据：每条命令与退出码、每次调用的 JSON 与失败时的 stderr、每张截图（-raw 是抓到的原始像素）、一张数字表，落在 tests/evidence/<时间戳>/。它只驱动自己的冒烟靶子。两条写测量时必须遵守的规则也记在这里：读取区域里不能留文字光标（会闪、会让整行读崩），区域高度要盖住整行。
      
  en: >
      Turns one measurement run into evidence a human can re-read: every command with its exit code, the JSON of every call plus stderr on failure, every screenshot (-raw is the pixels as captured) and a table of numbers, under tests/evidence/<timestamp>/. It only drives its own smoke target. The two rules it enforces: keep the text caret out of the read region (it blinks, and a frame with it can misread the whole line), and make the region cover the whole line box.
      
revision: df4356ba04850d65df87f2c6f6d4e83f34b0f709
updated_at: "2026-10-03T09:21:00.898Z"
fingerprint: 82c7ca94e4d2cc1bdcc71302b688f99198f8e3ca3e76fe6efed0cfba816488b0
source:
  - path: "tests/evidence.ps1"
apis:
  - protocol: rpc
    path: "测量证据归档"
    description:
      zh: >
          跑一遍测量并把命令 / JSON / 截图 / 汇总全部落盘。
          
      en: >
          Runs the battery and archives commands, JSON, screenshots and a summary.
          
deps:
  - kind: call
    to: keymouse.pick.overlay
    label: {zh: "驱动选区浮层", en: "Drive the overlay"}
  - kind: call
    to: keymouse.tests.smoke-target
    label: {zh: "启动靶子窗口", en: "Start the target window"}
  - kind: reference
    to: keymouse.probe.preprocess
    to_api: "rpc:Probe.Preprocess"
    label: {zh: "量取样与预处理", en: "Measure the sampler"}
---
