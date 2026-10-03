---
uid: 7a1f0e03
id: keymouse.runner.host
parent: keymouse.runner
tags: [runner]
name: {zh: "作业与派发", en: "Jobs and dispatch"}
description:
  zh: >
      请求分发（run/record/validate/pick-region/ocr/控制/状态）、作业注册表与 `Execution.Control` 的实现：作业自己就是那个接缝，于是暂停、取消、逐步事件都发生在能力层的执行路径上。日志流用的是把 Console.Out 接到事件汇上——诚实标注：这只有在作业串行时才成立，把 Core 与 Console 解耦是下一步。
      
  en: >
      Request dispatch (run/record/validate/pick-region/ocr/control/status), the job registry, and the implementation of `Execution.Control`: the job *is* the seam, so pausing, cancelling and per-step events happen on the capability layer execution path. The log stream works by wiring Console.Out to the event sink - honest note: that only holds because jobs are serialised, and decoupling Core from Console is the next step.
      
revision: 1ebae14ff2430b597cc4a1695a71ddf788879db1
updated_at: "2026-10-03T12:49:24.517Z"
fingerprint: c6e16f99d7a79b0fb4e78d7f4e1ace595af81bd22299b254ee89e15b2ddfe900
source:
  - path: "src/KeyMouse.Runner/RunnerHost.cs"
  - path: "src/KeyMouse.Runner/RunnerJob.cs"
apis:
  - protocol: rpc
    path: "RunnerHost.HandleAsync"
    description:
      zh: >
          处理一个请求并把事件写回客户端。
          
      en: >
          Handles one request and writes its events back.
          
  - protocol: rpc
    path: "RunnerHost.Run"
    description:
      zh: >
          `serve` 的入口：监听直到被要求退出。
          
      en: >
          The `serve` entry point: listen until asked to stop.
          
  - protocol: rpc
    path: "RunnerJob.BetweenSteps"
    description:
      zh: >
          暂停闸门与取消点（抛 CommandFailure(7)）。
          
      en: >
          The pause gate and the cancel point (throws CommandFailure(7)).
          
deps:
  - kind: call
    to: keymouse.cli.main
    to_api: "rpc:keymouse <命令组>"
    label: {zh: "驱动同一份派发", en: "Drives the same dispatch"}
  - kind: call
    to: keymouse.execution
    to_api: "rpc:Execution.Control"
    label: {zh: "装上执行接缝", en: "Installs the execution seam"}
---
