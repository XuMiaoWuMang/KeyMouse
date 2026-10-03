---
uid: 9f0a1b2c
id: keymouse.console.width
parent: keymouse.console
tags: [console, i18n]
name: {zh: "显示宽度计算", en: "Display width"}
description:
  zh: >
      中文一个字占两个终端格但只算一个 char，用 PadRight 排表会逐列漂移（中文越多偏得越多）。这里按终端格宽计算并补白/截断，让 window list 的列对齐。
      
  en: >
      A CJK glyph occupies two terminal cells but is one char, so PadRight drifts one column per Chinese character. Width is measured in terminal cells here so window-list columns line up.
      
revision: 6347fbfbc3d7af8839f5dcc9c35501ce2484bdef
updated_at: "2026-10-03T11:11:56.198Z"
fingerprint: 7b34359e8593529cec65ec16fb52cabdc1ce2520355312e65521683c77dfcee0
source:
  - path: "src/KeyMouse.Core/ConsoleText.cs"
    line: 46
    end_line: 86
apis:
  - protocol: rpc
    path: "ConsoleText.DisplayWidth"
    description:
      zh: >
          字符串占多少终端格。
          
      en: >
          How many terminal cells a string takes.
          
  - protocol: rpc
    path: "ConsoleText.Pad"
    description:
      zh: >
          按格宽右补空格。
          
      en: >
          Pads by display width.
          
  - protocol: rpc
    path: "ConsoleText.Truncate"
    description:
      zh: >
          按格宽截断（带省略号）。
          
      en: >
          Truncates by display width.
          
---
