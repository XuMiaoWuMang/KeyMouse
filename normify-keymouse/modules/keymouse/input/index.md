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
      
revision: 6347fbfbc3d7af8839f5dcc9c35501ce2484bdef
updated_at: "2026-10-03T11:11:56.201Z"
fingerprint: 313ba7f64e5fc1c76991cd00fadad0b4c74007b47eb4e68d1954227536a95c29
source:
  - path: "src/KeyMouse.Core/NativeInput.cs"
    line: 1
    end_line: 274
deps:
  - kind: call
    to: keymouse.native
    label: {zh: "SendInput 声明", en: "SendInput declarations"}
---
