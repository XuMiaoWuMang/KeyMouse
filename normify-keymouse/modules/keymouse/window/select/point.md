---
uid: c9daebfc
id: keymouse.window.select.point
parent: keymouse.window.select
tags: [window, coordinates]
name: {zh: "屏幕点归属", en: "Screen point ownership"}
description:
  zh: >
      回答“这个屏幕坐标下是哪个窗口”与“这个点是否落在客户区内”——前者用于 --strict-point，后者用于拦截落在窗口装饰上的客户区坐标。
      
  en: >
      Answers which window is under a screen point, and whether a point is inside the client area; the first backs --strict-point, the second refuses client coordinates that land on window chrome.
      
revision: 6347fbfbc3d7af8839f5dcc9c35501ce2484bdef
updated_at: "2026-10-03T11:11:56.218Z"
fingerprint: dd1baf24ba788d3671c7613d3c370b5b1cf8f263253cd39ec29217d797b6df20
source:
  - path: "src/KeyMouse.Core/WindowLocator.cs"
    line: 56
    end_line: 74
apis:
  - protocol: rpc
    path: "WindowLocator.WindowAt"
    description:
      zh: >
          屏幕点下方的顶层窗口。
          
      en: >
          Top-level window under a screen point.
          
  - protocol: rpc
    path: "WindowLocator.IsInsideClientArea"
    description:
      zh: >
          点是否在客户区内。
          
      en: >
          Whether a point is inside the client area.
          
  - protocol: rpc
    path: "WindowLocator.ClientToScreen"
    description:
      zh: >
          客户区坐标→屏幕坐标。
          
      en: >
          Maps client coordinates to screen.
          
deps:
  - kind: call
    to: keymouse.native.user32-layer
    from_api: "rpc:WindowLocator.WindowAt"
    to_api: "rpc:user32!WindowFromPoint"
    label: {zh: "命中测试", en: "Hit test"}
  - kind: call
    to: keymouse.native.user32-geometry
    from_api: "rpc:WindowLocator.ClientToScreen"
    to_api: "rpc:user32!ClientToScreen"
    label: {zh: "坐标映射", en: "Coordinates"}
---
