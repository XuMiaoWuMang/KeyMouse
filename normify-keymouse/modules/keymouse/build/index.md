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
      
revision: d870b0c5dfd7fe29675863c0288b572a6111a8d4
updated_at: "2026-10-03T07:28:04.445Z"
fingerprint: 0a226e48dea6486acdb67bbed29779e47215fee34f8f9342a22b9017e999b346
source:
  - path: "build.ps1"
  - path: "verify.ps1"
  - path: ".github/workflows/ci.yml"
  - path: ".github/workflows/release.yml"
---
