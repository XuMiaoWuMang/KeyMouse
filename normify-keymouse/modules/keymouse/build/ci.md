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
      
revision: 6347fbfbc3d7af8839f5dcc9c35501ce2484bdef
updated_at: "2026-10-03T11:11:56.193Z"
fingerprint: cd3a724cf806ba4d601c783ff83ee96a0ff016d0f943524475810e3f10d0f8dd
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
