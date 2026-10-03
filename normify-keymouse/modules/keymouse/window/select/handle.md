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
      
revision: 6347fbfbc3d7af8839f5dcc9c35501ce2484bdef
updated_at: "2026-10-03T11:11:56.218Z"
fingerprint: dd1baf24ba788d3671c7613d3c370b5b1cf8f263253cd39ec29217d797b6df20
source:
  - path: "src/KeyMouse.Core/WindowLocator.cs"
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
