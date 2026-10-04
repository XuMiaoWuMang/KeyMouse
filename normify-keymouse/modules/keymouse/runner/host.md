---
uid: 7a1f0e03
id: keymouse.runner.host
parent: keymouse.runner
tags: [runner]
name: {zh: "作业与派发", en: "Jobs and dispatch"}
description:
  zh: >
      请求分发（run/record/validate/pick-region/ocr/控制/状态）、作业注册表与 Execution.Control 的实现：作业自己就是那个接缝，于是暂停、取消、逐步事件都发生在能力层的执行路径上。两条硬规矩：队列槽先拿、可能失败的事都在 try 里——校验曾经在拿槽之前，一抛异常作业就永远停在 queued，客户端只能干等；现在失败也一定回 error + finished。作业同时是运行证据的持有者：每步结束顺手留一张截图，并记下是谁在问。
      
  en: >
      Request dispatch (run, record, validate, pick-region, ocr, control, status), the job registry and Execution.Control: the job is the seam for pausing, cancelling and per-step events. The queue slot is taken first and everything that can fail sits inside the try - validation used to run before the slot, so one throw left a job queued forever and its client waiting; a failure now always answers error plus finished. The job also owns run evidence and records which client asked.
      
revision: ef3cf740e80bd62dd7540df6ee4c526380163233
updated_at: "2026-10-04T14:24:35.931Z"
fingerprint: 7d3f8ccb0f610fdb7878c125cffb62fd28b7807b2638deb038dfd5b666ff551f
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
          
  - protocol: rpc
    path: "RunnerJob.StepFinished"
    description:
      zh: >
          一步跑完：发事件，并顺手留一张证据截图。
          
      en: >
          A step finished: emits the event and leaves a screenshot as evidence.
          
  - protocol: rpc
    path: "RunnerJob.Info"
    description:
      zh: >
          list/status 用的作业信息：状态、退出码、耗时与是谁在问。
          
      en: >
          Job info for list/status: state, exit code, timing and who asked.
          
deps:
  - kind: call
    to: keymouse.cli.main
    to_api: "rpc:keymouse <命令组>"
    label: {zh: "驱动同一份派发", en: "Drives the same dispatch"}
  - kind: call
    to: keymouse.execution
    to_api: "rpc:Execution.Control"
    label: {zh: "装上执行接缝", en: "Installs the execution seam"}
  - kind: call
    to: keymouse.flow.evidence
    from_api: "rpc:RunnerJob.StepFinished"
    to_api: "rpc:RunShots.Capture"
    label: {zh: "留一张证据", en: "Leaves evidence"}
---
