---
uid: 7f3b9d21
id: keymouse.keys
parent: keymouse
tags: [keys]
name: {zh: "按键名映射", en: "Key name map"}
description:
  zh: >
      按键名 → 虚拟键码/是否扩展键：字母数字、F1-F24、方向键、修饰键、小键盘、符号键名，以及 vk:0x5B 这样的裸虚拟键逃生口。名字大小写不敏感，未知按键直接报错。
      
  en: >
      Maps key names to virtual-key codes and the extended-key flag: letters, F1-F24, arrows, modifiers, numpad, symbol names, plus the raw vk:0x5B escape hatch.
      
revision: 8138caf9efa304f3487fcf5c6320ee4ac39f036b
updated_at: "2026-10-03T10:11:54.003Z"
fingerprint: 25a839f445b22a732ec61ead6f2a2bcc99cff347f76627cd9fa8fc1b4e5b22cc
source:
  - path: "KeyMap.cs"
    line: 1
    end_line: 73
apis:
  - protocol: rpc
    path: "KeyMap.Resolve"
    description:
      zh: >
          按键名→虚拟键码（大小写不敏感）。
          
      en: >
          Key name to virtual-key code.
          
  - protocol: rpc
    path: "KeyMap.Names"
    description:
      zh: >
          全部可用键名（供帮助与错误提示）。
          
      en: >
          Every known key name.
          
---
