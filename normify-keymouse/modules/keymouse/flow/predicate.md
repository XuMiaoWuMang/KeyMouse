---
uid: 7a1f0c58
id: keymouse.flow.predicate
parent: keymouse.flow
tags: [flow, probe]
name: {zh: "文字判定谓词", en: "Text predicate"}
description:
  zh: >
      `wait-text` 的比较规则：contains / exact / fuzzy（编辑距离 ≤ 调用方声明的 maxErrors）。**比较时忽略空白，两侧都是**——引擎把中文按字切词并加空格，按字面比较会让用户照屏幕原文写的那串字永远匹配不上；这是比较规则，不是对读出来的文字做加工，probe 打印的仍是引擎原样输出。纯函数，可离线单测。
      
  en: >
      The comparison rule behind `wait-text`: contains / exact / fuzzy (edit distance within the budget the caller declares). Whitespace is ignored on both sides - the engine splits CJK into one word per character, so a literal comparison would never match the text a user can see; that is a comparison rule, not post-processing of the read, and `probe` still prints the engine output verbatim. Pure and unit-tested.
      
revision: 6347fbfbc3d7af8839f5dcc9c35501ce2484bdef
updated_at: "2026-10-03T11:11:56.201Z"
fingerprint: e96e540fbb668ddd4c0fab445eae2f32bfd12ccc80d0ae9fc107796e6ce59cbe
source:
  - path: "src/KeyMouse.Core/TextPredicate.cs"
apis:
  - protocol: rpc
    path: "TextPredicate.Matches"
    description:
      zh: >
          读到的东西是否算等到了（并给出差几个字符的解释）。
          
      en: >
          Whether what was read counts as the wait being over, with an explanation of the near miss.
          
  - protocol: rpc
    path: "TextPredicate.Distance"
    description:
      zh: >
          编辑距离：容错预算以字符计，就用字符量。
          
      en: >
          Edit distance: the budget is stated in characters, so it is measured in them.
          
  - protocol: rpc
    path: "TextPredicate.Squash"
    description:
      zh: >
          比较用的去空白形式。
          
      en: >
          The whitespace-free form used for every comparison.
          
  - protocol: rpc
    path: "TextPredicate.IsKnownMode"
    description:
      zh: >
          加载时校验 match 模式是否认识。
          
      en: >
          Validates a match mode at load time.
          
---
