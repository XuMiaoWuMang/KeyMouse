---
uid: 2d3e4f5a
id: keymouse.cli.mouse.geometry
parent: keymouse.cli.mouse
tags: [mouse, coordinates]
name: {zh: "坐标换算与归属校验", en: "Coordinate mapping & ownership"}
description:
  zh: >
      客户区坐标 → 屏幕坐标的统一入口，并在三处拒绝含糊情形：客户区坐标落在窗口外、把客户区坐标和绝对坐标混用、--strict-point 下目标点下方的窗口不是目标本身。
      
  en: >
      One path from client coordinates to screen coordinates, and three refusals: a point outside the client area, mixing client and absolute coordinates, and --strict-point finding another window under the point.
      
revision: ef3cf740e80bd62dd7540df6ee4c526380163233
updated_at: "2026-10-04T14:22:17.411Z"
fingerprint: c2466d777f9e13ef4b2681d5d98fa1383d4339672611d90ef8fad5764cbbc7fc
source:
  - path: "src/KeyMouse.Core/Commands.cs"
    line: 161
    end_line: 168
  - path: "src/KeyMouse.Core/Commands.cs"
    line: 342
    end_line: 349
apis:
  - protocol: rpc
    path: "RelativePoint"
    description:
      zh: >
          客户区坐标转屏幕坐标。
          
      en: >
          Maps client coordinates to screen.
          
  - protocol: rpc
    path: "RequirePointBelongsToTarget"
    description:
      zh: >
          --strict-point 归属校验。
          
      en: >
          The --strict-point ownership check.
          
  - protocol: rpc
    path: "RejectAbsolute"
    description:
      zh: >
          拒绝客户区与绝对坐标混用。
          
      en: >
          Refuses mixing client and absolute.
          
deps:
  - kind: call
    to: keymouse.window
    label: {zh: "客户区/命中测试", en: "Client rect & hit test"}
---
