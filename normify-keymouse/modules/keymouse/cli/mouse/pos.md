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
      
revision: 8d69164cfe5fa0896f595c0bca51f97977ff51b9
updated_at: "2026-10-02T17:37:21.449Z"
fingerprint: ed066abe077cf617475b89420866941ec0b7c26b595508435b3db3daac6799d5
source:
  - path: "Program.cs"
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
