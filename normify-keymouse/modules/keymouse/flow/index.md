---
uid: 7a1f0c53
id: keymouse.flow
parent: keymouse
tags: [flow, json, cli]
name: {zh: "JSON 流程", en: "JSON flow"}
description:
  zh: >
      `keymouse-flow` v1：录制产物与手写/编辑的载体。它刻意不是第二套执行语义——每一步都会编译成一条人类会敲的命令，因此选择器、焦点闸门、退出码与 `--dry-run` / `--report` / `--retry` 全部与手打命令一致；只有 sleep / wait-window 这类没有命令行等价物的步骤由执行器自己处理。
      
  en: >
      `keymouse-flow` v1: what recording writes and what a human edits. Deliberately not a second execution semantic - every step compiles into a command a human would type, so selectors, the focus gate, exit codes and `--dry-run` / `--report` / `--retry` mean the same thing as when typed by hand; only steps without a command-line equivalent (sleep, wait-window) are handled by the runner itself.
      
revision: 69321dbdf0d2f94c72a77144dd1c35e063d13815
updated_at: "2026-10-03T11:41:01.204Z"
fingerprint: 89aca4c57048ffb3c1b4df69b8245e2bbbc095bfccc3986afef099bea0ce1f69
source:
  - path: "src/KeyMouse.Core/FlowModel.cs"
  - path: "src/KeyMouse.Core/FlowRunner.cs"
---
