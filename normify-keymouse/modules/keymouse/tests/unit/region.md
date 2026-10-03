---
uid: 7a1f0c40
id: keymouse.tests.unit.region
parent: keymouse.tests.unit
tags: [test, unit]
name: {zh: "选区坐标换算单测", en: "Region placement unit tests"}
description:
  zh: >
      不需要桌面的那一半：--rect 的解析与拒绝、坐标系判定的边界（正好等于客户区、比客户区宽一像素、跨出窗口、整屏选区），以及建议命令的文本。浮层本身要人（或模拟输入），在冒烟里测。
      
  en: >
      The half that needs no desktop: parsing and rejecting --rect, the boundaries of the space rule (exactly the client area, one pixel wider, crossing the window, a whole-screen selection), and the text of the suggested command. The overlay itself needs a human or simulated input and lives in smoke.
      
revision: e03d53e4c41a8a7220e83123d7f8a44a54ff8dab
updated_at: "2026-10-03T08:15:51.236Z"
fingerprint: b7e85c7feace83b6e350352d8c604c454cb1874a6d6dced340228eab5b9c24af
source:
  - path: "tests/KeyMouse.Tests/RegionTests.cs"
apis:
  - protocol: rpc
    path: "选区坐标换算断言组"
    description:
      zh: >
          --rect 解析与坐标系判定。
          
      en: >
          --rect parsing and the space rule.
          
deps:
  - kind: reference
    to: keymouse.pick.placement
    to_api: "rpc:RegionCommand.Classify"
    label: {zh: "被测的判定规则", en: "The rule under test"}
---
