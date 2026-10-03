---
uid: a3b4c5d6
id: keymouse.build
parent: keymouse
tags: [build, ci]
name: {zh: "构建与发布", en: "Build & release"}
description:
  zh: >
      单文件发布脚本、发布前四道关卡的一键验证入口，以及 CI / Release 两条工作流。发布由 v* tag 触发，且必须先经人工跑通 verify.ps1。
      
  en: >
      The single-file publish script, a one-command pre-release verification entry, and the CI/release workflows. Publishing is tag-driven and gated on a human running verify.ps1 first.
      
revision: 6347fbfbc3d7af8839f5dcc9c35501ce2484bdef
updated_at: "2026-10-03T11:13:17.047Z"
fingerprint: 0ff7d0958875da898313069f9a1a9cf996d3935c59f42b48056209ec4f7216f6
source:
  - path: "build.ps1"
  - path: "verify.ps1"
  - path: ".github/workflows/ci.yml"
  - path: ".github/workflows/release.yml"
---
