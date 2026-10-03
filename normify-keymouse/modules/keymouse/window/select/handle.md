---
uid: daebfc0d
id: keymouse.window.select.handle
parent: keymouse.window.select
tags: [window, parsing]
name: {zh: "句柄解析", en: "Handle parsing"}
description:
  zh: >
      --hwnd 接受 0x 前缀或十进制，解析后校验窗口仍存在；非法输入给出可读错误而不是抛异常。
      
  en: >
      --hwnd accepts hex with a 0x prefix or decimal, checks the window still exists, and reports a readable error for bad input instead of throwing.
      
revision: d870b0c5dfd7fe29675863c0288b572a6111a8d4
updated_at: "2026-10-03T07:28:04.469Z"
fingerprint: e52dac08492f5ae3f8efd6a9e9cbe6e5af9a49c1c1402c8c9bedaf366fa57b9c
source:
  - path: "WindowLocator.cs"
    line: 75
    end_line: 96
apis:
  - protocol: rpc
    path: "WindowLocator.ParseHandle"
    description:
      zh: >
          解析 --hwnd（0x 或十进制）。
          
      en: >
          Parses --hwnd as hex or decimal.
          
---
