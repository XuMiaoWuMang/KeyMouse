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
      
revision: 6347fbfbc3d7af8839f5dcc9c35501ce2484bdef
updated_at: "2026-10-03T11:11:56.201Z"
fingerprint: 313ba7f64e5fc1c76991cd00fadad0b4c74007b47eb4e68d1954227536a95c29
source:
  - path: "src/KeyMouse.Core/NativeInput.cs"
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
