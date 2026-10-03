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
      
revision: 7a1a124eb7b20fcbebed310d012cbf3926b69a0b
updated_at: "2026-10-03T08:34:07.493Z"
fingerprint: 7c55943e80c562d7dcb0b775f6833af391ec21be069a3c75b57c6dd97cff24b5
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
