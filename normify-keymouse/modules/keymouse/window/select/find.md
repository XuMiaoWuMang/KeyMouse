---
uid: b8c9daeb
id: keymouse.window.select.find
parent: keymouse.window.select
tags: [window]
name: {zh: "匹配执行", en: "Match execution"}
description:
  zh: >
      枚举一次+过滤一次，返回全部匹配（不在这里选一个）——歧义交给上层报错并要求 --pick。
      
  en: >
      One enumeration, one filter, returning every match; picking among them is left to the caller, which refuses and asks for --pick instead of guessing.
      
revision: 6347fbfbc3d7af8839f5dcc9c35501ce2484bdef
updated_at: "2026-10-03T11:11:56.217Z"
fingerprint: dd1baf24ba788d3671c7613d3c370b5b1cf8f263253cd39ec29217d797b6df20
source:
  - path: "src/KeyMouse.Core/WindowLocator.cs"
    line: 52
    end_line: 55
apis:
  - protocol: rpc
    path: "WindowLocator.Find"
    description:
      zh: >
          返回匹配选择器的全部窗口。
          
      en: >
          Returns every window matching the selector.
          
deps:
  - kind: call
    to: keymouse.window.select.enumerate
    from_api: "rpc:WindowLocator.Find"
    to_api: "rpc:WindowLocator.EnumerateTopLevel"
    label: {zh: "先枚举", en: "Enumerate"}
  - kind: call
    to: keymouse.window.select.matcher
    from_api: "rpc:WindowLocator.Find"
    to_api: "rpc:WindowSelector.Matches"
    label: {zh: "再过滤", en: "Then filter"}
---
