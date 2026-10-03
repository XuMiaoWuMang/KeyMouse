---
uid: d38ee3f4
id: keymouse.build.ci
parent: keymouse.build
tags: [ci]
name: {zh: "CI 工作流", en: "CI workflow"}
description:
  zh: >
      每次 push/PR：构建整个解决方案、跑单元测试、查文档链接、再做一次发布构建（保证发布命令本身没坏）。CI 不跑桌面冒烟——runner 没有交互式桌面。
      
  en: >
      On every push and PR: build the solution, run the unit suite, check doc links, and do a publish build so the release command itself cannot rot. CI does not run the smoke suite - its runners have no interactive desktop.
      
revision: e03d53e4c41a8a7220e83123d7f8a44a54ff8dab
updated_at: "2026-10-03T08:15:19.841Z"
fingerprint: 23148afbab4c70db6fa2b78b991f70c63a69a7138e5e0516f8b622309abfaa46
source:
  - path: ".github/workflows/ci.yml"
    line: 1
    end_line: 32
apis:
  - protocol: rpc
    path: "ci.yml (push)"
    description:
      zh: >
          push 即跑的构建与单元测试。
          
      en: >
          Build and unit tests on every push.
          
deps:
  - kind: call
    to: keymouse.tests.unit.parsing.entry
    from_api: "rpc:ci.yml (push)"
    to_api: "rpc:ParsingTests.Run"
    label: {zh: "跑单元测试", en: "Run unit tests"}
  - kind: call
    to: keymouse.tests.docs-link
    from_api: "rpc:ci.yml (push)"
    to_api: "rpc:pwsh tests/check-docs.ps1"
    label: {zh: "查链接", en: "Check links"}
---
