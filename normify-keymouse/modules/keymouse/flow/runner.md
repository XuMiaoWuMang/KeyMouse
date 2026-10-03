---
uid: 7a1f0c55
id: keymouse.flow.runner
parent: keymouse.flow
tags: [flow, cli]
name: {zh: "流程执行器", en: "Flow runner"}
description:
  zh: >
      执行一个流程：逐步编译成命令交给同一份派发，失败即停或 `--keep-going`，可重试退出码按 `--retry`，报告与文本脚本同形。每步之间经过执行接缝（暂停阻塞、取消抛退出码 7、边界作为事件上报）。文本条件共用同一个轮询：`wait-text` 等它、`click-text` 等到后点匹配框中心、`when` 不成立则跳过（报告标 skipped）或按 `else: fail` 退出码 3。
      
  en: >
      Runs a flow: steps are compiled into commands for the same dispatch, stopping at the first failure or honouring --keep-going, retrying the retryable codes per --retry. Between steps it passes the execution seam (a pause blocks, a cancel throws exit code 7, boundaries arrive as events). All text conditions share one polling loop: wait-text waits, click-text clicks the middle of the matched box, and an unmet when skips the step or fails with exit 3 - none of the three can send anything extra.
      
revision: 1b316650150f1540368ee548f7d9992ccaa3239e
updated_at: "2026-10-03T11:27:19.954Z"
fingerprint: bd2b72f94f0675e0c622926b05b796ab39043aa3fb52c11067521d302fd74254
source:
  - path: "src/KeyMouse.Core/FlowRunner.cs"
apis:
  - protocol: rpc
    path: "FlowRunner.LooksLikeJson"
    description:
      zh: >
          按内容判断 run 拿到的是不是流程。
          
      en: >
          Content-based detection of a flow handed to run.
          
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
    to: keymouse.flow.predicate
    to_api: "rpc:TextPredicate.Matches"
    label: {zh: "判定等到了没有", en: "Decide if the wait is over"}
  - kind: call
    to: keymouse.probe.locate
    to_api: "rpc:TextLocator.Find"
    label: {zh: "找文字并给出框", en: "Locate text and its box"}
  - kind: call
    to: keymouse.probe.engine
    to_api: "rpc:Probe.ReadOnce"
    label: {zh: "每轮读一次区域", en: "One read per poll"}
  - kind: call
    to: keymouse.execution
    to_api: "rpc:Execution.Control"
    label: {zh: "等待期间也能被取消", en: "Cancellable while waiting"}
---
