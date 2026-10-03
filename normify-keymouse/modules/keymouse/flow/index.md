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
      
revision: 89f30aa8db66e03f9c60253d85abc64d7760319e
updated_at: "2026-10-03T10:20:29.618Z"
fingerprint: f27d4f0b2636751b2128b373a2bb41959b6f8c644eea836083948017ca54b332
source:
  - path: "FlowModel.cs"
  - path: "FlowRunner.cs"
---
