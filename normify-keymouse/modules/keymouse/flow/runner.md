---
uid: 7a1f0c55
id: keymouse.flow.runner
parent: keymouse.flow
tags: [flow, cli]
name: {zh: "流程执行器", en: "Flow runner"}
description:
  zh: >
      `run flow.json`：按顺序执行步骤，每一步都走与手打命令相同的派发（含闸门与退出码），失败即停或 `--keep-going`，可重试的退出码按 `--retry` 重试，报告与文本脚本同一形状。sleep 在 `--dry-run` 下不真等；wait-window 轮询到窗口可用；wait-text 每轮一次 OCR，连续 confirm 次读到同一段满足条件的文字才算等到，超时退出码 3 并说明最后读到什么。
      
  en: >
      `run flow.json`: executes steps in order, each through the same dispatch as a typed command (gate and exit codes included), stopping at the first failure or honouring --keep-going, retrying the retryable codes per --retry, and writing the same report shape as the text runner. sleep does not wait under --dry-run; wait-window polls for a usable window; wait-text reads once per poll and counts a match only after `confirm` consecutive identical reads, exiting 3 on timeout with what it read.
      
revision: 89f30aa8db66e03f9c60253d85abc64d7760319e
updated_at: "2026-10-03T10:20:36.520Z"
fingerprint: 43b42b8b5255e7029c9b4c0d9f929e71f833ad1cfeda1e9dd76eec1d9f31b10c
source:
  - path: "FlowRunner.cs"
apis:
  - protocol: rpc
    path: "FlowRunner.LooksLikeJson"
    description:
      zh: >
          按内容判断 run 拿到的是不是流程（只认第一个参数是路径的形式）。
          
      en: >
          Content-based detection of a flow handed to run (only the documented first-argument form).
          
  - protocol: rpc
    path: "FlowRunner.Run"
    description:
      zh: >
          执行整个流程并返回退出码。
          
      en: >
          Runs the whole flow and returns its exit code.
          
deps:
  - kind: call
    to: keymouse.flow.model
    to_api: "rpc:FlowDocument.ToArguments"
    label: {zh: "步骤编译成命令", en: "Compile a step"}
  - kind: call
    to: keymouse.cli.main
    label: {zh: "同一条派发路径", en: "The same dispatch"}
---
