---
uid: c7d8e9fa
id: keymouse.cli.mouse.pos
parent: keymouse.cli.mouse
tags: [mouse]
name: {zh: "光标坐标查询", en: "Cursor position query"}
description:
  zh: >
      打印当前光标屏幕坐标（x,y），不发送任何事件；也是脚本里确认“刚才那步真的移动了”的常用回读手段。
      
  en: >
      Prints the current cursor position as x,y without sending anything; the usual way a script reads back whether the previous step really moved the pointer.
      
revision: ef3cf740e80bd62dd7540df6ee4c526380163233
updated_at: "2026-10-04T14:22:17.411Z"
fingerprint: c2466d777f9e13ef4b2681d5d98fa1383d4339672611d90ef8fad5764cbbc7fc
source:
  - path: "src/KeyMouse.Core/Commands.cs"
    line: 182
    end_line: 188
apis:
  - protocol: rpc
    path: "mouse pos"
    description:
      zh: >
          输出光标坐标。
          
      en: >
          Prints the cursor position.
          
deps:
  - kind: call
    to: keymouse.input.send
    from_api: "rpc:mouse pos"
    to_api: "rpc:NativeInput.GetCursor"
    label: {zh: "读光标", en: "Read cursor"}
---
