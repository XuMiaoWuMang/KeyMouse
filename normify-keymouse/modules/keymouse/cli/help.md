---
uid: a5b6c7d8
id: keymouse.cli.help
parent: keymouse.cli
tags: [docs, cli]
name: {zh: "内置帮助文本", en: "Built-in help text"}
description:
  zh: >
      一份随版本打包的中文帮助：命令表、选择器与策略说明、退出码含义。用插值原始字符串写成，版本号从常量注入。
  en: >
      The version-packed help text: command tables, selector and policy notes, exit-code meanings. Written as an interpolated raw string with the version constant injected.
revision: 8d69164cfe5fa0896f595c0bca51f97977ff51b9
updated_at: "2026-10-02T17:55:00Z"
fingerprint: 63379135dc36b913fa3f42f5262c67e5bdac49a9dfa6d3ff0e2931487a7cc4b3
source:
  - path: "Usage.cs"
    line: 1
    end_line: 128
apis:
  - protocol: rpc
    path: "Usage.Print"
    description:
      zh: >
          输出帮助文本。
      en: >
          Writes the help text.
---
