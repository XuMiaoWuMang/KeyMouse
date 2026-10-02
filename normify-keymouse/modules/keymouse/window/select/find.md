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
      
revision: 8d69164cfe5fa0896f595c0bca51f97977ff51b9
updated_at: "2026-10-02T17:37:21.450Z"
fingerprint: e52dac08492f5ae3f8efd6a9e9cbe6e5af9a49c1c1402c8c9bedaf366fa57b9c
source:
  - path: "WindowLocator.cs"
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
