---
uid: 8192a3b4
id: keymouse.console
parent: keymouse
tags: [console, i18n]
name: {zh: "控制台文本与帮助", en: "Console text & help"}
description:
  zh: >
      控制台的两件小事：按控制台码页输出（否则中文在 cp936 终端会变成乱码），以及按显示宽度而非字符数补白/截断（CJK 字占两格，PadRight 会让表格逐列漂移）。另有内置帮助文本。
      
  en: >
      Two console concerns: speak the console code page (UTF-8 bytes into a cp936 terminal is mojibake), and pad/truncate by display width because a CJK glyph is two cells wide. Also holds the built-in help text.
      
revision: 8138caf9efa304f3487fcf5c6320ee4ac39f036b
updated_at: "2026-10-03T10:11:54.002Z"
fingerprint: eaeb85e79e9ba8710dbb026e1fc3df0bae858efa3a68148fae4e0d70fc476609
source:
  - path: "ConsoleText.cs"
    line: 1
    end_line: 86
  - path: "Usage.cs"
    line: 1
    end_line: 128
deps:
  - kind: call
    to: keymouse.native
    label: {zh: "读取控制台码页", en: "Read console code page"}
---
