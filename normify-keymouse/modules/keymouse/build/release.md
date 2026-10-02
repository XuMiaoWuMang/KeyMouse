---
uid: e49ff4a5
id: keymouse.build.release
parent: keymouse.build
tags: [ci, release]
name: {zh: "Release 工作流", en: "Release workflow"}
description:
  zh: >
      推 v* tag 才触发：先跑单元测试（不过就不发），再构两个产物（框架依赖 ~240 KB 与自包含 ~36 MB）并附到同名 Release。发布不由 push 触发，且必须先经人工验证。
      
  en: >
      Triggered only by a v* tag: run the unit suite (a failure blocks the release), build both artefacts (framework-dependent ~240 KB and self-contained ~36 MB) and attach them to the release. A push never publishes.
      
revision: 8d69164cfe5fa0896f595c0bca51f97977ff51b9
updated_at: "2026-10-02T17:38:44.180Z"
fingerprint: 5ffdae790b856196f341ea38d42e20c030c62ea7bc55d88ceba98ddbc2ffee49
source:
  - path: ".github/workflows/release.yml"
    line: 1
    end_line: 45
apis:
  - protocol: rpc
    path: "release.yml (v* tag)"
    description:
      zh: >
          tag → 双产物 → Release。
          
      en: >
          Tag to two artefacts attached to a release.
          
deps:
  - kind: call
    to: keymouse.build.publish
    from_api: "rpc:release.yml (v* tag)"
    to_api: "rpc:.\\build.ps1"
    label: {zh: "构产物", en: "Build artefacts"}
  - kind: call
    to: keymouse.tests.unit.parsing.entry
    from_api: "rpc:release.yml (v* tag)"
    to_api: "rpc:ParsingTests.Run"
    label: {zh: "先跑测试", en: "Test first"}
---
