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
      
revision: 6347fbfbc3d7af8839f5dcc9c35501ce2484bdef
updated_at: "2026-10-03T11:13:17.048Z"
fingerprint: f0afb9c88f544f9dd71bd8ceedb29f555d58848202faa27f7b3ed7f2c43d43d3
source:
  - path: "src/KeyMouse.Core/FlowRunner.cs"
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
