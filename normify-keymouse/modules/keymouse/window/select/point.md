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
      
revision: d870b0c5dfd7fe29675863c0288b572a6111a8d4
updated_at: "2026-10-03T07:28:04.469Z"
fingerprint: e52dac08492f5ae3f8efd6a9e9cbe6e5af9a49c1c1402c8c9bedaf366fa57b9c
source:
  - path: "WindowLocator.cs"
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
