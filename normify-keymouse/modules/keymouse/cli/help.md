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
      
revision: 6347fbfbc3d7af8839f5dcc9c35501ce2484bdef
updated_at: "2026-10-03T11:11:56.195Z"
fingerprint: 7d2aaaca86d67355fa8f5733fa6016e537b1b30a8a48cf1602066cb3149364db
source:
  - path: "src/KeyMouse.Core/Usage.cs"
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
