---
uid: 7a1f0c55
id: keymouse.flow.runner
parent: keymouse.flow
tags: [flow, cli]
name: {zh: "流程执行器", en: "Flow runner"}
description:
  zh: >
      执行一个流程：先展平循环，再逐步解析变量、编译成命令交给同一份派发；失败即停或 `--keep-going`，可重试退出码按 `--retry`。每步之间经过执行接缝（暂停阻塞、取消抛退出码 7、边界作为事件上报）。文本条件共用同一个轮询：`wait-text` 等它、`click-text` 点到匹配框中心、`when` 不成立则跳过或退出码 3；`read-text` 把读到的内容存进变量。
      
  en: >
      Runs a flow: loops are flattened first, then each step has its variables resolved and is compiled into a command for the same dispatch; stopping at the first failure or honouring --keep-going, retrying the retryable codes. Between steps it passes the execution seam (a pause blocks, a cancel throws exit code 7, boundaries come as events). Text conditions share one polling loop: wait-text waits, click-text clicks the matched box, an unmet when skips or exits 3, and read-text stores a value.
      
revision: 69321dbdf0d2f94c72a77144dd1c35e063d13815
updated_at: "2026-10-03T11:41:09.823Z"
fingerprint: 19384591c36518462d5983b183e3fb009cb9f24772db85f4d3c63cdd169cc213
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
    to_api: "rpc:FlowDocument.ExpandLoops"
    label: {zh: "先展平循环", en: "Flatten the loops"}
  - kind: call
    to: keymouse.flow.model
    to_api: "rpc:FlowDocument.Resolve"
    label: {zh: "再解析变量", en: "Then resolve variables"}
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
    label: {zh: "读一次区域（变量来源）", en: "One read (value source)"}
  - kind: call
    to: keymouse.execution
    to_api: "rpc:Execution.Control"
    label: {zh: "等待期间也能被取消", en: "Cancellable while waiting"}
---
