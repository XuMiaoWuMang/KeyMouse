---
uid: 1c7d8e9f
id: keymouse.input.text
parent: keymouse.input
tags: [input, unicode]
name: {zh: "Unicode 文本注入", en: "Unicode text injection"}
description:
  zh: >
      逐字符发送 KEYEVENTF_UNICODE：绕过键盘布局与输入法，因此中文、日文、emoji 一样能打；字符之间留间隔，末尾额外留一段排空时间，避免最后一个字丢失。
      
  en: >
      Sends KEYEVENTF_UNICODE per character, bypassing layout and IMEs so Chinese, Japanese or emoji all work; a per-character interval plus a trailing drain keeps the last character from being lost.
      
revision: e03d53e4c41a8a7220e83123d7f8a44a54ff8dab
updated_at: "2026-10-03T08:15:19.858Z"
fingerprint: 3618318b51206f21111b1abedb3cc5ef20caa74b67c90f6583a8fea20b8a2783
source:
  - path: "NativeInput.cs"
    line: 249
    end_line: 274
apis:
  - protocol: rpc
    path: "NativeInput.TypeText"
    description:
      zh: >
          逐字输入一段文本。
          
      en: >
          Types a string character by character.
          
deps:
  - kind: call
    to: keymouse.input.send
    from_api: "rpc:NativeInput.TypeText"
    to_api: "rpc:NativeInput.Send"
    label: {zh: "按批投递", en: "Batched dispatch"}
---
