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
revision: 8d69164cfe5fa0896f595c0bca51f97977ff51b9
updated_at: "2026-10-02T17:55:00Z"
fingerprint: ed066abe077cf617475b89420866941ec0b7c26b595508435b3db3daac6799d5
source:
  - path: "Program.cs"
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
