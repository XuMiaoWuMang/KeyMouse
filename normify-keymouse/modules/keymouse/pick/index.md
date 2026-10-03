---
uid: 7a1f0c3d
id: keymouse.pick
parent: keymouse
tags: [perception, interactive, cli]
name: {zh: "交互式选区", en: "Interactive region pick"}
description:
  zh: >
      让人用鼠标指出"读哪一块"，而不是凭记忆报四个数字（实测那样挑了三次，三次落在空白上）。浮层把屏幕矩形翻译成 probe 认得的坐标系：整个落在客户区就用客户区坐标，落在窗口矩形里就用窗口坐标（标题栏），两者都不是就只报屏幕坐标并说明原因。它只报坐标，一个字都不读。
      
  en: >
      Lets a human point at the region instead of reporting four numbers from memory (that landed on blank space three times). The overlay turns a screen rectangle into a space probe accepts: client coordinates when it fits the client area, window coordinates for a title bar, and screen coordinates plus a reason when it fits neither. It reports coordinates and reads nothing.
      
revision: e03d53e4c41a8a7220e83123d7f8a44a54ff8dab
updated_at: "2026-10-03T08:15:51.236Z"
fingerprint: 853d857ebeb734d91a8dac20797372f29757d753fb24b2e24a19741e8f07dede
source:
  - path: "RegionCommand.cs"
  - path: "RegionPicker.cs"
---
