---
uid: 9e0f1a2b
id: keymouse.cli.window.list
parent: keymouse.cli.window
tags: [window, diagnostics]
name: {zh: "窗口列表", en: "Window list"}
description:
  zh: >
      打印顶层窗口表（句柄/进程/标题/类名/状态），支持 --filter 与 --process 过滤，默认只列可见且有标题的，--all 连隐藏的辅助窗口一起列；先筛后探测，只对要打印的行做 WM_NULL 探测。
      
  en: >
      Prints the top-level window table, filtered by --filter/--process, visible-and-titled only unless --all; it filters first and only probes the rows it is about to print.
      
revision: 6347fbfbc3d7af8839f5dcc9c35501ce2484bdef
updated_at: "2026-10-03T11:11:56.198Z"
fingerprint: 7f2ba5c7c2569f29daf25ca2416e5988b42986f24461e2467265c7fae3dc41e9
source:
  - path: "src/KeyMouse.Core/Commands.cs"
    line: 441
    end_line: 464
apis:
  - protocol: rpc
    path: "window list"
    description:
      zh: >
          列出顶层窗口及其状态。
          
      en: >
          Lists top-level windows and their state.
          
deps:
  - kind: call
    to: keymouse.window
    label: {zh: "枚举与探测", en: "Enumerate & probe"}
  - kind: call
    to: keymouse.console
    label: {zh: "按显示宽度排表", en: "Width-aware table"}
---
