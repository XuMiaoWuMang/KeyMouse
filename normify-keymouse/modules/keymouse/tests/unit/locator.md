---
uid: 7a1f0f02
id: keymouse.tests.unit.locator
parent: keymouse.tests.unit
tags: [test, unit]
name: {zh: "文字定位单测", en: "Text location tests"}
description:
  zh: >
      不需要桌面：单词匹配、跨词并集、整行 exact、fuzzy 的容错与预算边界、没找到与空期望、框并集（含空集合），以及"行框是并集而不是第一个词的框"这条曾经悄悄错过的规则。
      
  en: >
      No desktop needed: single-word matches, cross-word unions, whole-line exact, the fuzzy budget and its boundaries, absent and empty expectations, box unions (empty included), and the rule that a line box is the union of its words rather than the first word box - a rule that used to be quietly wrong.
      
revision: 1b316650150f1540368ee548f7d9992ccaa3239e
updated_at: "2026-10-03T11:27:19.953Z"
fingerprint: 6602bc8f6b54fb034945c21f236848c2bfcb016f8f29bf664be9a728a58d0947
source:
  - path: "tests/KeyMouse.Tests/LocatorTests.cs"
apis:
  - protocol: rpc
    path: "文字定位断言组"
    description:
      zh: >
          定位与框并集的边界。
          
      en: >
          Locating and box-union boundaries.
          
deps:
  - kind: reference
    to: keymouse.probe.locate
    to_api: "rpc:TextLocator.Find"
    label: {zh: "被测的定位器", en: "Locator under test"}
---
