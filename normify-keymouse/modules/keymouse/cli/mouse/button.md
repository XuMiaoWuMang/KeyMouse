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
      
revision: 8d69164cfe5fa0896f595c0bca51f97977ff51b9
updated_at: "2026-10-02T17:37:21.449Z"
fingerprint: ed066abe077cf617475b89420866941ec0b7c26b595508435b3db3daac6799d5
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
