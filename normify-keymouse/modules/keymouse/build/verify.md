---
uid: c27dd2e3
id: keymouse.build.verify
parent: keymouse.build
tags: [build, gate]
name: {zh: "发布前验证入口", en: "Pre-release verification"}
description:
  zh: >
      一条命令跑完四道关卡（构建/单元/文档链接/打包/桌面冒烟）并给出结论；会识别锁屏并跳过冒烟并说明原因（锁屏时谁抢不到前台都是环境问题，不是产品问题）。末尾还列出自动关卡查不出的三项手动确认。
      
  en: >
      One command runs every gate and prints a verdict; it detects a locked desktop, skips the smoke run and says why. It also lists the three manual checks no automated gate can see.
      
revision: e03d53e4c41a8a7220e83123d7f8a44a54ff8dab
updated_at: "2026-10-03T08:15:19.844Z"
fingerprint: 4b60b8b09cf6410207fa23ffbbd761329601de5377024ed617512fdebd688b2e
source:
  - path: "verify.ps1"
    line: 1
    end_line: 139
apis:
  - protocol: rpc
    path: ".\\verify.ps1"
    description:
      zh: >
          跑完全部关卡并给出结论。
          
      en: >
          Runs every gate and prints a verdict.
          
  - protocol: rpc
    path: ".\\verify.ps1 -SkipSmoke"
    description:
      zh: >
          只跑不需要桌面的部分。
          
      en: >
          Runs only the desktop-free gates.
          
deps:
  - kind: call
    to: keymouse.build.publish
    from_api: "rpc:.\\verify.ps1"
    to_api: "rpc:.\\build.ps1"
    label: {zh: "打包", en: "Publish"}
  - kind: call
    to: keymouse.tests.unit.parsing.entry
    from_api: "rpc:.\\verify.ps1"
    to_api: "rpc:ParsingTests.Run"
    label: {zh: "单元测试", en: "Unit tests"}
  - kind: call
    to: keymouse.tests.docs-link
    from_api: "rpc:.\\verify.ps1"
    to_api: "rpc:pwsh tests/check-docs.ps1"
    label: {zh: "文档链接", en: "Docs links"}
  - kind: call
    to: keymouse.tests.smoke.entry
    from_api: "rpc:.\\verify.ps1"
    to_api: "rpc:pwsh tests/smoke.ps1"
    label: {zh: "桌面冒烟", en: "Desktop smoke"}
---
