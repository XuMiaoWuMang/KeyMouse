---
uid: 7a1f0c42
id: keymouse.tests.evidence
parent: keymouse.tests
tags: [test, desktop, evidence]
name: {zh: "测量留档", en: "Evidence archive"}
description:
  zh: >
      把一次测量变成可回看的证据：每条命令与退出码、每次调用的 JSON 与失败时的 stderr、每张截图（-raw 是抓到的原始像素）、一张数字表，落在 tests/evidence/<时间戳>/。**归档不入库**（.gitignore 忽略：一次 20 样本的扫描就是 10 MB 级）。它只驱动自己的冒烟靶子。两条写测量时必须遵守的规则也记在这里：读取区域里不能留文字光标（会闪、会让整行读崩），区域高度要盖住整行。
      
  en: >
      Turns one measurement run into evidence a human can re-read: every command with its exit code, the JSON of every call plus stderr on failure, every screenshot (-raw is the pixels as captured) and a table of numbers, under tests/evidence/<timestamp>/. Not tracked (gitignore: one 20-sample run is ~10 MB). It drives only its own smoke target, and records the two rules that keep a measurement honest: no text caret inside the read region, and the region must cover the whole line box.
      
revision: 85e950e25a3270a308e88673382b52020c16d9c6
updated_at: "2026-10-03T09:42:10.581Z"
fingerprint: 9acd2ba58f98ff67ec2cf02c40f46d225cd2d26e8ba0996f9b86e8cdb6636e39
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
