---
uid: 7a1f0c55
id: keymouse.flow.runner
parent: keymouse.flow
tags: [flow, cli]
name: {zh: "流程执行器", en: "Flow runner"}
description:
  zh: >
      执行一个流程：先展平循环、内联子流程，再逐步按帧解析变量、编译成命令交给同一份派发；失败即停或 `--keep-going`，可重试退出码按 `--retry`。每步之间经过执行接缝（暂停阻塞、取消抛退出码 7、边界作为事件上报），编号按**实际步数**（`call` 的导出项是账目，不算一步）。文本条件共用同一个轮询：`wait-text` 等它、`click-text` 点到匹配框中心、`when` 不成立则跳过或退出码 3；`read-text` 把读到的东西存进当前帧。
      
  en: >
      Runs a flow: loops expanded and subflows inlined first, then each step resolves its variables against its frame and is compiled for the same dispatch; stopping at the first failure or honouring --keep-going, retrying the retryable codes. Between steps it passes the execution seam and numbers steps by what really runs (a call export is bookkeeping). Text conditions share one polling loop, and read-text stores into the current frame.
      
revision: 1ebae14ff2430b597cc4a1695a71ddf788879db1
updated_at: "2026-10-03T12:49:24.517Z"
fingerprint: b085a45e1dbf1489536fcaea4135b549c5a73ac74a5fb51d0883088f929203d6
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
    to: keymouse.flow.plan
    to_api: "rpc:FlowPlan.Build"
    label: {zh: "展平并内联子流程", en: "Flatten and inline calls"}
  - kind: call
    to: keymouse.flow.model
    to_api: "rpc:FlowDocument.Resolve"
    label: {zh: "按帧解析变量", en: "Resolve variables per frame"}
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
