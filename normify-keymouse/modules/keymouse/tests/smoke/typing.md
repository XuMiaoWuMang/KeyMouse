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
      
revision: 1b316650150f1540368ee548f7d9992ccaa3239e
updated_at: "2026-10-03T11:27:16.011Z"
fingerprint: 2ab5b1e29a47f02cf1bf9588824895c65511b838f298921b03517b68cf0c2879
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
