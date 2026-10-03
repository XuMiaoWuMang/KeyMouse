---
uid: 2b3c4d5e
id: keymouse.cli
parent: keymouse
tags: [cli]
name: {zh: "命令行入口与派发", en: "CLI entry & dispatch"}
description:
  zh: >
      把 argv 变成一次输入事件：提取全局选项 → 按命令组派发 → 把异常映射成退出码。命令组为 mouse / key / window / run / help，选择器与坐标策略在这里落到 GlobalOptions 上。
      
  en: >
      Turns argv into one input event: extract global options, dispatch by command group, map exceptions to exit codes. Groups: mouse / key / window / run / help.
      
revision: 6347fbfbc3d7af8839f5dcc9c35501ce2484bdef
updated_at: "2026-10-03T11:13:17.046Z"
fingerprint: 7f2ba5c7c2569f29daf25ca2416e5988b42986f24461e2467265c7fae3dc41e9
source:
  - path: "src/KeyMouse.Core/Commands.cs"
    line: 1
    end_line: 658
deps:
  - kind: call
    to: keymouse.input
    label: {zh: "注入事件", en: "Inject events"}
  - kind: call
    to: keymouse.window
    label: {zh: "解析并验证目标", en: "Resolve & verify target"}
  - kind: call
    to: keymouse.script
    label: {zh: "执行脚本", en: "Run a script"}
  - kind: call
    to: keymouse.keys
    label: {zh: "按键名解析", en: "Resolve key names"}
  - kind: call
    to: keymouse.console
    label: {zh: "表格与帮助输出", en: "Tables & help output"}
---
