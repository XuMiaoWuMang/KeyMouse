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
      
revision: 8138caf9efa304f3487fcf5c6320ee4ac39f036b
updated_at: "2026-10-03T10:12:03.456Z"
fingerprint: 9645ae0ab6eb4ce394724b190e9325811c2bf44f429d8dc628ea2d25e7dca361
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
