---
uid: 293b4d5f
id: keymouse.tests.smoke.typing
parent: keymouse.tests.smoke
tags: [test, desktop]
name: {zh: "打字往返、拖拽与变量", en: "Typing, drag & variables"}
description:
  zh: >
      最核心的一组：把文本打进靶子再回读剪贴板逐字比对、窗口相对拖拽是否真的选中了文本、${} 变量是否真的把光标移到了替代后的坐标。
      
  en: >
      The core group: type into the target and read the clipboard back to compare, check that a window-relative drag really selected text, and that a substituted variable really moved the cursor to those coordinates.
      
revision: 7a1a124eb7b20fcbebed310d012cbf3926b69a0b
updated_at: "2026-10-03T08:34:07.495Z"
fingerprint: ab0b75ca45752a0a184daf193e8dfd4433f2d690df694dd17c7f347a5e4d374a
source:
  - path: "tests/smoke.ps1"
    line: 77
    end_line: 166
apis:
  - protocol: rpc
    path: "打字与拖拽断言组"
    description:
      zh: >
          输入真的落到了窗口里的证明。
          
      en: >
          Proof that input really landed in the window.
          
deps:
  - kind: call
    to: keymouse.cli.main
    from_api: "rpc:打字与拖拽断言组"
    to_api: "rpc:keymouse <命令组>"
    label: {zh: "驱动被测命令", en: "Drive the command"}
---
