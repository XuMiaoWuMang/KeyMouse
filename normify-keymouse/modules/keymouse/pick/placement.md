---
uid: 7a1f0c3f
id: keymouse.pick.placement
parent: keymouse.pick
tags: [perception, cli, coordinates]
name: {zh: "屏幕矩形 → 窗口坐标系", en: "Screen rectangle to window space"}
description:
  zh: >
      把屏幕矩形落到它中心下方的窗口上，并按一条规则翻译坐标：整个落在客户区里 → client（窗口一动就不失效的那套）；落在窗口矩形里 → window（标题栏在客户区之外，只有它能表达）；两者都不是 → 只报屏幕坐标 + 原因，不给可粘贴的命令。输出里附带一条可直接粘贴的 probe 调用。--rect 是给脚本和测试的非交互入口，走同一套换算。
      
  en: >
      Lands a screen rectangle on the window under its centre and translates it by one rule: inside the client area -> client (the space that survives moving the window), inside the window rectangle -> window (a title bar lives outside the client area), neither -> screen coordinates plus a reason and no paste-ready command. The output carries a ready-to-paste probe invocation. --rect is the non-interactive entry point scripts and tests use, through the same conversion.
      
revision: e03d53e4c41a8a7220e83123d7f8a44a54ff8dab
updated_at: "2026-10-03T08:15:51.236Z"
fingerprint: 91a40c4a47543ebd1a1c94898f55da242b0c5eac7597a30d477c8eb304a771a8
source:
  - path: "RegionCommand.cs"
    line: 1
    end_line: 167
apis:
  - protocol: rpc
    path: "RegionCommand.Run"
    description:
      zh: >
          region 组的入口：--rect 或浮层，然后报坐标。
          
      en: >
          Entry point of the region group: --rect or the overlay, then report.
          
  - protocol: rpc
    path: "RegionCommand.ParseRect"
    description:
      zh: >
          解析 --rect 的 x,y,w,h（屏幕坐标），宽高必须为正。
          
      en: >
          Parses --rect x,y,w,h in screen pixels, rejecting non-positive sizes.
          
  - protocol: rpc
    path: "RegionCommand.Classify"
    description:
      zh: >
          坐标系判定本身，纯函数、可单测。
          
      en: >
          The space rule itself, a pure function the unit tests drive.
          
  - protocol: rpc
    path: "RegionCommand.Describe"
    description:
      zh: >
          解析选区下方的窗口并给出坐标系与建议命令。
          
      en: >
          Resolves the window under the selection and reports space plus suggested command.
          
  - protocol: rpc
    path: "RegionCommand.ClientBounds"
    description:
      zh: >
          客户区在屏幕上的矩形（0x0 客户区的壳窗口回退成窗口矩形）。
          
      en: >
          The client area in screen pixels, falling back to the window rectangle for client-less shell windows.
          
deps:
  - kind: call
    to: keymouse.window.select.point
    from_api: "rpc:RegionCommand.Describe"
    to_api: "rpc:WindowLocator.WindowAt"
    label: {zh: "选区中心下方的窗口", en: "Window under the centre"}
  - kind: call
    to: keymouse.window.select.point
    from_api: "rpc:RegionCommand.ClientBounds"
    to_api: "rpc:WindowLocator.ClientToScreen"
    label: {zh: "客户区原点的屏幕坐标", en: "Client origin on screen"}
---
