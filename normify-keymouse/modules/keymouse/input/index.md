---
uid: 4d5e6f70
id: keymouse.input
parent: keymouse
tags: [input]
name: {zh: "输入注入", en: "Input injection"}
description:
  zh: >
      把命令翻译成 SendInput 事件：光标移动（绝对/相对）、鼠标键、滚轮、拖拽、单键与组合键、以及不依赖输入法的 Unicode 文本注入。所有事件一次性投递进系统输入队列，与真人操作走同一条路。
      
  en: >
      Translates commands into SendInput events: cursor movement, mouse buttons, wheels, drag, keys and chords, and layout-independent Unicode text. Events land in the system input queue exactly like real ones.
      
revision: e03d53e4c41a8a7220e83123d7f8a44a54ff8dab
updated_at: "2026-10-03T08:15:19.857Z"
fingerprint: 3618318b51206f21111b1abedb3cc5ef20caa74b67c90f6583a8fea20b8a2783
source:
  - path: "NativeInput.cs"
    line: 1
    end_line: 274
deps:
  - kind: call
    to: keymouse.native
    label: {zh: "SendInput 声明", en: "SendInput declarations"}
---
