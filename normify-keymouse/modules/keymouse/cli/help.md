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
      
revision: 96c306061d63c034a01c2369e4abc295251e2f35
updated_at: "2026-10-03T13:37:44.310Z"
fingerprint: a2cfe9e632206485327c509915ce0070a177a1c96cdf354b77421538e65d0b6b
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
