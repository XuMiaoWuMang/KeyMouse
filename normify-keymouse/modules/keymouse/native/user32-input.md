---
uid: 5d7e9f1b
id: keymouse.native.user32-input
parent: keymouse.native
tags: [win32, input]
name: {zh: "输入 API 与结构体", en: "Input APIs & structs"}
description:
  zh: >
      SendInput 及其配套：INPUT/MOUSEINPUT/KEYBDINPUT 结构体布局、光标读写、虚拟屏尺寸与虚拟键映射。结构体布局错一个字节，事件就会静默变形，所以这里是全项目最不容许“差不多”的地方。
      
  en: >
      SendInput and its companions: the INPUT/MOUSEINPUT/KEYBDINPUT layouts, cursor reads and writes, virtual-screen metrics and virtual-key mapping. One wrong byte in a layout silently reshapes events, so nothing here may be approximate.
      
revision: d870b0c5dfd7fe29675863c0288b572a6111a8d4
updated_at: "2026-10-03T07:28:04.454Z"
fingerprint: 3618318b51206f21111b1abedb3cc5ef20caa74b67c90f6583a8fea20b8a2783
source:
  - path: "NativeInput.cs"
    line: 78
    end_line: 94
apis:
  - protocol: rpc
    path: "user32!SendInput"
    description:
      zh: >
          投递一批输入事件。
          
      en: >
          Posts a batch of input events.
          
  - protocol: rpc
    path: "user32!GetCursorPos"
    description:
      zh: >
          读光标位置。
          
      en: >
          Reads the cursor position.
          
  - protocol: rpc
    path: "user32!SetCursorPos"
    description:
      zh: >
          直接设置光标位置。
          
      en: >
          Sets the cursor position directly.
          
  - protocol: rpc
    path: "user32!MapVirtualKey"
    description:
      zh: >
          虚拟键→扫描码（扩展键推断）。
          
      en: >
          Virtual key to scan code.
          
  - protocol: rpc
    path: "user32!GetSystemMetrics"
    description:
      zh: >
          虚拟屏尺寸（绝对坐标归一化）。
          
      en: >
          Virtual screen metrics for absolute mapping.
          
---
