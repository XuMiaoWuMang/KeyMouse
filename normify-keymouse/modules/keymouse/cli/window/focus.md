---
uid: bf2c3d4e
id: keymouse.cli.window.focus
parent: keymouse.cli.window
tags: [window, focus]
name: {zh: "聚焦命令", en: "Focus command"}
description:
  zh: >
      只做“选目标 + 抢前台 + 回读验证”三件事，不发送任何输入；成功则打印实际选中的窗口。脚本里它同时把当前目标设为该窗口，供后续 mouse/key 行继承。
      
  en: >
      Picks a target, takes the foreground and verifies it, sending nothing. On success it prints the chosen window and becomes the script's current target for later mouse/key lines.
      
revision: d870b0c5dfd7fe29675863c0288b572a6111a8d4
updated_at: "2026-10-03T07:28:04.449Z"
fingerprint: 2002c7c0806c2cc60e9cdeb156af20af0b5bd3ede859541c1f12dbf68f0b71ac
source:
  - path: "Program.cs"
    line: 497
    end_line: 545
apis:
  - protocol: rpc
    path: "window focus"
    description:
      zh: >
          聚焦目标并验证（不发输入）。
          
      en: >
          Focuses and verifies without sending input.
          
deps:
  - kind: call
    to: keymouse.cli.window.targeting
    from_api: "rpc:window focus"
    to_api: "rpc:PrepareTarget"
    label: {zh: "选目标 + 聚焦策略", en: "Target + focus policy"}
  - kind: call
    to: keymouse.window.resolve
    from_api: "rpc:window focus"
    to_api: "rpc:WindowResolver.Resolve"
    label: {zh: "解析与验证", en: "Resolve & verify"}
---
