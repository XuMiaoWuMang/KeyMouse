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
      
revision: 6347fbfbc3d7af8839f5dcc9c35501ce2484bdef
updated_at: "2026-10-03T11:11:56.204Z"
fingerprint: fbbe01c854130cbabcde458cb5ce11bce55a3ab38b5e0c98c3b84082fc7bb9c9
source:
  - path: "src/KeyMouse.Core/RegionCommand.cs"
  - path: "src/KeyMouse.Core/RegionPicker.cs"
---
