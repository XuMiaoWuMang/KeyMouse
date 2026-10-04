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
      
revision: ef3cf740e80bd62dd7540df6ee4c526380163233
updated_at: "2026-10-04T14:22:17.409Z"
fingerprint: c2466d777f9e13ef4b2681d5d98fa1383d4339672611d90ef8fad5764cbbc7fc
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
