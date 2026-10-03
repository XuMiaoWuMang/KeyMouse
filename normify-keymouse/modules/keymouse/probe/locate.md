---
uid: 7a1f0f01
id: keymouse.probe.locate
parent: keymouse.probe
tags: [probe, perception]
name: {zh: "文字定位", en: "Text location"}
description:
  zh: >
      把"读到的字"变成"可点的框"：整行压成一个串并记住每个字符来自哪个词（引擎按字切词，逐词匹配永远找不到"保存"），contains 取区间并集、exact 要整行、fuzzy 扫长度±预算的窗口取编辑距离最小者。纯函数，框的并集与跨词匹配都能离线单测——它是 `--find` 与 `click-text` 的共同地基。
      
  en: >
      Turns what was read into a box that can be clicked: the line is squashed into one string that remembers which word each character came from (CJK arrives per character, so word-by-word matching would never find a two-character word); contains takes the union of the range, exact demands the whole line, and fuzzy scans windows of length +/- the budget for the smallest edit distance. Pure and unit-tested - it is what --find and click-text both stand on.
      
revision: 1b316650150f1540368ee548f7d9992ccaa3239e
updated_at: "2026-10-03T11:27:19.953Z"
fingerprint: f1a6b41ededfadf88865ca8abb07ece17941e0abcfd0f9c37501aea7294616f0
source:
  - path: "src/KeyMouse.Core/TextLocator.cs"
apis:
  - protocol: rpc
    path: "TextLocator.Find"
    description:
      zh: >
          在读到的东西里定位一段文字，返回它的框与差几个字符。
          
      en: >
          Locates a piece of text in what was read, returning its box and how far off it was.
          
  - protocol: rpc
    path: "TextLocator.Union"
    description:
      zh: >
          多个词框的并集（行框与跨词匹配都用它）。
          
      en: >
          The union of several word boxes (used for line boxes and cross-word matches).
          
deps:
  - kind: call
    to: keymouse.flow.predicate
    to_api: "rpc:TextPredicate.Distance"
    label: {zh: "容错预算按字符算", en: "Budget counted in characters"}
---
