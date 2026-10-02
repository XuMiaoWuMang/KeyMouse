---
uid: fa5b6c7d
id: keymouse.input.drag
parent: keymouse.input
tags: [input, mouse]
name: {zh: "拖拽序列", en: "Drag sequence"}
description:
  zh: >
      按下 → 分步移动 → 抬起，分步数与总时长可控：一次跳过去很多应用不认，分步才能让目标窗口看到连续移动。
      
  en: >
      Press, move in steps, release, with step count and duration: a single jump is ignored by many applications, so stepping is what makes the target see a real drag.
      
revision: 8d69164cfe5fa0896f595c0bca51f97977ff51b9
updated_at: "2026-10-02T17:37:21.449Z"
fingerprint: 3618318b51206f21111b1abedb3cc5ef20caa74b67c90f6583a8fea20b8a2783
source:
  - path: "NativeInput.cs"
    line: 193
    end_line: 215
apis:
  - protocol: rpc
    path: "NativeInput.Drag"
    description:
      zh: >
          从起点拖到终点。
          
      en: >
          Drags from one point to another.
          
deps:
  - kind: call
    to: keymouse.input.send
    from_api: "rpc:NativeInput.Drag"
    to_api: "rpc:NativeInput.Send"
    label: {zh: "同批投递", en: "Ordered batch"}
---
