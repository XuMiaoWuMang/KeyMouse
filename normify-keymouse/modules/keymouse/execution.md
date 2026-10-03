---
uid: 7a1f0e05
id: keymouse.execution
parent: keymouse
tags: [core, runner]
name: {zh: "执行接缝", en: "Execution seam"}
description:
  zh: >
      能力层唯一知道"有人可能在看着这次执行"的地方：一个接口（暂停闸门、取消、逐步上报）+ 一个静态挂点。CLI 不挂它，于是同一段流程代码就是一次普通阻塞调用；Runner 挂上作业对象，于是同一条路径就有了暂停/取消/逐步事件。取消抛的是 Core 自己的 CommandFailure(7)——实测抛 OperationCanceledException 会被顶层兜底变成退出码 1。
      
  en: >
      The only place in the capability layer that knows someone might be watching: one interface (pause gate, cancel, per-step reporting) plus a static hook. The CLI installs nothing, so the same flow code is a plain blocking call; the Runner installs a job, so the same path gains pause, cancel and per-step events. Cancelling throws Core own CommandFailure(7): measurably, an OperationCanceledException surfaced as exit code 1 instead.
      
revision: 6347fbfbc3d7af8839f5dcc9c35501ce2484bdef
updated_at: "2026-10-03T11:13:17.045Z"
fingerprint: 885f00e153df78d28fc59f2e6f08c35af1ccec56cdb6f1d0442d2fe32971eb2d
source:
  - path: "src/KeyMouse.Core/ExecutionControl.cs"
apis:
  - protocol: rpc
    path: "Execution.Control"
    description:
      zh: >
          当前执行的控制点（null = 没人看着）。
          
      en: >
          The control point of the current execution (null = nobody watching).
          
  - protocol: rpc
    path: "IExecutionControl.BetweenSteps"
    description:
      zh: >
          步与步之间：暂停时阻塞，取消时抛退出码 7。
          
      en: >
          Between steps: blocks while paused, throws exit code 7 when cancelled.
          
---
