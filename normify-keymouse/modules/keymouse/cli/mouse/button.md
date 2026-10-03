---
uid: fa0b1c2d
id: keymouse.cli.mouse.button
parent: keymouse.cli.mouse
tags: [mouse]
name: {zh: "点击、双击与按住", en: "Click, double-click & hold"}
description:
  zh: >
      click / dblclick / down / up 四个子命令共用一套参数：键（left/right/middle/x1/x2）、次数、间隔，以及可选的 -x/-y 落点（先移动再按）。双击等价于 click -n 2。
      
  en: >
      click / dblclick / down / up share one set of arguments: which button, how many, interval, and an optional point to move to first. Double-click is click -n 2.
      
revision: d870b0c5dfd7fe29675863c0288b572a6111a8d4
updated_at: "2026-10-03T07:28:04.448Z"
fingerprint: 2002c7c0806c2cc60e9cdeb156af20af0b5bd3ede859541c1f12dbf68f0b71ac
source:
  - path: "Program.cs"
    line: 220
    end_line: 260
apis:
  - protocol: rpc
    path: "mouse click"
    description:
      zh: >
          单击（可指定键/次数/落点）。
          
      en: >
          Clicks (button, count and point optional).
          
  - protocol: rpc
    path: "mouse dblclick"
    description:
      zh: >
          双击。
          
      en: >
          Double-clicks.
          
  - protocol: rpc
    path: "mouse down"
    description:
      zh: >
          按下不放（用于手动拖拽）。
          
      en: >
          Presses and holds a button.
          
  - protocol: rpc
    path: "mouse up"
    description:
      zh: >
          松开鼠标键。
          
      en: >
          Releases a button.
          
deps:
  - kind: call
    to: keymouse.input.mouse-button
    from_api: "rpc:mouse click"
    to_api: "rpc:NativeInput.Click"
    label: {zh: "点击", en: "Click"}
---
