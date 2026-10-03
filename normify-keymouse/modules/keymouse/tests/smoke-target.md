---
uid: 8e3a9eaf
id: keymouse.tests.smoke-target
parent: keymouse.tests
tags: [test, desktop]
name: {zh: "冒烟靶子窗口", en: "Smoke target window"}
description:
  zh: >
      一个整块客户区都是文本框的 WinForms 窗口，供冒烟测试驱动。存在理由：以前借记事本，既要先杀掉用户所有记事本才能确定哪扇是自己开的，又会碰上 Win11 记事本恢复标签页导致“同一个窗口”变成两扇、选择器直接歧义。
      
  en: >
      A WinForms window whose whole client area is a text box, driven by the smoke suite. It exists because borrowing Notepad meant killing every Notepad to be sure which window was ours, and Win11's tab restore could turn 'the' window into two and make every selector ambiguous.
      
revision: d870b0c5dfd7fe29675863c0288b572a6111a8d4
updated_at: "2026-10-03T07:28:04.464Z"
fingerprint: 0ed48c8d7e475cebfcb023a72ba2db689a3f7cab21fc241ddd19ebb7ff567e5d
source:
  - path: "tests/KeyMouse.SmokeTarget/Program.cs"
    line: 1
    end_line: 46
apis:
  - protocol: rpc
    path: "KeyMouse.SmokeTarget.exe"
    description:
      zh: >
          开一扇可被驱动的靶子窗口。
          
      en: >
          Opens a driveable target window.
          
---
