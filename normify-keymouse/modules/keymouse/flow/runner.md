---
uid: 7a1f0c55
id: keymouse.flow.runner
parent: keymouse.flow
tags: [flow, cli]
name: {zh: "流程执行器", en: "Flow runner"}
description:
  zh: >
      执行一个流程：先展平循环、内联子流程，再逐步按帧解析变量、编译成命令交给同一份派发；失败即停或 `--keep-going`，可重试退出码按 `--retry`。编号按实际步数，文本条件共用一个轮询。文档里的 `allowRestore` 会**跟着编译出的命令行一起走**（步骤交给同一个命令行解析器执行，它有自己对"能不能还原"的判断——这里踩过坑：只在运行器里设标志仍然退出码 4）。
      
  en: >
      Runs a flow: loops expanded and subflows inlined first, then each step resolves its variables against its frame and is compiled for the same dispatch; stopping at the first failure or honouring --keep-going, retrying the retryable codes. Between steps it passes the execution seam, numbers steps by what really runs, and all text conditions share one polling loop. The document allowRestore travels with the compiled command line.
      
revision: ef3cf740e80bd62dd7540df6ee4c526380163233
updated_at: "2026-10-04T14:24:35.929Z"
fingerprint: e556e9ac89ec074dbb9cfa7c693edea6d8600a7d9359172b06415af522ff8c16
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
