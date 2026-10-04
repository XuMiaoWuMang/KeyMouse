---
uid: 3e4f5a6b
id: keymouse.cli.keyboard
parent: keymouse.cli
tags: [keyboard]
name: {zh: "键盘子命令组", en: "Keyboard command group"}
description:
  zh: >
      key 下的全部子命令：敲击（可连按）、按住/松开、组合键、文本输入。后两者分别解决“多个键同时按下”和“输入任意 Unicode”两件事。
      
  en: >
      Every key subcommand: press (optionally repeated), hold/release, chords, and text input. The last two cover pressing keys together and typing arbitrary Unicode.
      
revision: ef3cf740e80bd62dd7540df6ee4c526380163233
updated_at: "2026-10-04T14:22:17.409Z"
fingerprint: c2466d777f9e13ef4b2681d5d98fa1383d4339672611d90ef8fad5764cbbc7fc
source:
  - path: "src/KeyMouse.Core/Commands.cs"
    line: 350
    end_line: 430
deps:
  - kind: call
    to: keymouse.input
    label: {zh: "注入键盘事件", en: "Inject key events"}
  - kind: call
    to: keymouse.keys
    label: {zh: "按键名→虚拟键码", en: "Key name to VK"}
  - kind: call
    to: keymouse.cli.window.targeting
    label: {zh: "选目标并聚焦", en: "Pick and focus target"}
---
