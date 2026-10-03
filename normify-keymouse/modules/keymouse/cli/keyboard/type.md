---
uid: 7c8d9e0f
id: keymouse.cli.keyboard.type
parent: keymouse.cli.keyboard
tags: [keyboard, unicode]
name: {zh: "文本输入", en: "Text input"}
description:
  zh: >
      key type <文本> [--interval 毫秒]：用 Unicode 注入逐字输入，不依赖输入法/键盘布局，因此中文与任意字符都能打；默认为每字 15 毫秒，并留出收尾宽限让最后一个字不被吞。
      
  en: >
      key type <text> [--interval ms] types character by character through Unicode injection, so it is layout- and IME-independent; the default 15 ms per character plus a drain grace keeps the last character from being lost.
      
revision: 01b8c3c91b12f7561f62aa9f6af36e7eb9e3bb94
updated_at: "2026-10-03T10:33:15.211Z"
fingerprint: bbcd909665ff033ce01c238b5437796aded55168ead822ef453ff228abd0fee5
source:
  - path: "Program.cs"
    line: 414
    end_line: 430
apis:
  - protocol: rpc
    path: "key type"
    description:
      zh: >
          输入文本（含中文）。
          
      en: >
          Types text, Chinese included.
          
deps:
  - kind: call
    to: keymouse.input.text
    from_api: "rpc:key type"
    to_api: "rpc:NativeInput.TypeText"
    label: {zh: "Unicode 逐字", en: "Unicode per char"}
---
