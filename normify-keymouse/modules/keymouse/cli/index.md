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
revision: 8d69164cfe5fa0896f595c0bca51f97977ff51b9
updated_at: "2026-10-02T17:50:00Z"
fingerprint: ed066abe077cf617475b89420866941ec0b7c26b595508435b3db3daac6799d5
source:
  - path: "Program.cs"
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
