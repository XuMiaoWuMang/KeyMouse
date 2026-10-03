---
uid: bc2d3e4f
id: keymouse.input.send
parent: keymouse.input
tags: [input]
name: {zh: "批量投递与计数", en: "Batch dispatch & counting"}
description:
  zh: >
      所有注入的唯一出口：一次 SendInput 投递整批事件（鼠标按下→移动→抬起必须同批，否则会被系统重排），并记录发了多少、抑制了多少（--dry-run 下计入抑制数）。
      
  en: >
      The single exit for every injection: one SendInput call per batch, because press-move-release must stay ordered, while counting what was sent and what was suppressed under --dry-run.
      
revision: e03d53e4c41a8a7220e83123d7f8a44a54ff8dab
updated_at: "2026-10-03T08:15:19.858Z"
fingerprint: 3618318b51206f21111b1abedb3cc5ef20caa74b67c90f6583a8fea20b8a2783
source:
  - path: "NativeInput.cs"
    line: 95
    end_line: 132
apis:
  - protocol: rpc
    path: "NativeInput.Send"
    description:
      zh: >
          投递一批输入事件。
          
      en: >
          Dispatches a batch of input events.
          
  - protocol: rpc
    path: "NativeInput.GetCursor"
    description:
      zh: >
          读当前光标坐标。
          
      en: >
          Reads the current cursor position.
          
deps:
  - kind: call
    to: keymouse.native.user32-input
    from_api: "rpc:NativeInput.Send"
    to_api: "rpc:user32!SendInput"
    label: {zh: "SendInput", en: "SendInput"}
  - kind: call
    to: keymouse.script.execution-mode
    from_api: "rpc:NativeInput.Send"
    to_api: "rpc:ExecutionMode.NoteSuppressed"
    label: {zh: "演练与计数", en: "Dry-run & counters"}
---
