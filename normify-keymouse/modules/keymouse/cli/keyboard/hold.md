---
uid: 5a6b7c8d
id: keymouse.cli.keyboard.hold
parent: keymouse.cli.keyboard
tags: [keyboard]
name: {zh: "按住与松开", en: "Hold & release"}
description:
  zh: >
      key down / key up：单独的按下与抬起，用来手写组合、长按或跨命令保持按住状态。脚本里两条命令可以夹着其它操作。
      
  en: >
      key down / key up: separate press and release, for hand-rolled combinations, long holds, or keeping a key down across other commands in a script.
      
revision: 6347fbfbc3d7af8839f5dcc9c35501ce2484bdef
updated_at: "2026-10-03T11:11:56.195Z"
fingerprint: 7f2ba5c7c2569f29daf25ca2416e5988b42986f24461e2467265c7fae3dc41e9
source:
  - path: "src/KeyMouse.Core/Commands.cs"
    line: 380
    end_line: 399
apis:
  - protocol: rpc
    path: "key down"
    description:
      zh: >
          按住一个键。
          
      en: >
          Holds a key down.
          
  - protocol: rpc
    path: "key up"
    description:
      zh: >
          松开一个键。
          
      en: >
          Releases a key.
          
deps:
  - kind: call
    to: keymouse.input.key
    from_api: "rpc:key down"
    to_api: "rpc:NativeInput.Key"
    label: {zh: "单键下/上", en: "Key down/up"}
---
