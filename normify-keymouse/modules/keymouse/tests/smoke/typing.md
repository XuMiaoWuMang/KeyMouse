---
uid: 293b4d5f
id: keymouse.tests.smoke.typing
parent: keymouse.tests.smoke
tags: [test, desktop]
name: {zh: "打字往返、拖拽与变量", en: "Typing, drag & variables"}
description:
  zh: >
      冒烟模块 typing：打字往返（真发键、回读剪贴板）、窗口相对拖拽、脚本变量替换后光标真的到位。
      
  en: >
      Smoke module typing: a real typing round trip (keys sent, clipboard read back), a window-relative drag, and script variables that really move the cursor.
      
revision: bc06440df10935e9e8479ecad7b52098b48df7fd
updated_at: "2026-10-03T12:19:12.569Z"
fingerprint: 4ea9f21d04f113d9bb6824ea8ba3b2a5e39576be39e78ef2850d86830eda45b8
source:
  - path: "tests/smoke/typing.ps1"
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
