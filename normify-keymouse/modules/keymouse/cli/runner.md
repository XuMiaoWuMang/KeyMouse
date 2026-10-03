---
uid: 7a1f0e07
id: keymouse.cli.runner
parent: keymouse.cli
tags: [cli, runner]
name: {zh: "CLI 侧的 Runner 客户端", en: "Runner client on the CLI side"}
description:
  zh: >
      `runner status | list | run | cancel | pause | resume | stop`：命令行既能一次性执行，也能驱动同一个常驻引擎——这就是"两个入口跑同一份执行体"的另一半。没有引擎在听时给退出码 4 并告诉你先跑 `serve`。
      
  en: >
      `runner status | list | run | cancel | pause | resume | stop`: the command line can either execute once or drive the same resident engine - the other half of "two entry points, one execution body". With nothing listening it exits 4 and tells you to start `serve` first.
      
revision: 6347fbfbc3d7af8839f5dcc9c35501ce2484bdef
updated_at: "2026-10-03T11:13:17.045Z"
fingerprint: 50042784145b55d2cbdbe49b1d28cb4bc0356a4366171deb7a5dbdd2e571b1da
source:
  - path: "src/KeyMouse.Cli/RunnerCommand.cs"
apis:
  - protocol: rpc
    path: "keymouse runner run"
    description:
      zh: >
          让常驻引擎执行一个流程，并把过程打印出来。
          
      en: >
          Has the resident engine execute a flow and prints what happens.
          
  - protocol: rpc
    path: "keymouse runner status"
    description:
      zh: >
          问引擎状态与作业表。
          
      en: >
          Asks the engine for its state and job table.
          
deps:
  - kind: call
    to: keymouse.runner.pipe
    to_api: "rpc:RunnerClient.SendAsync"
    label: {zh: "一条连接发请求", en: "One connection, many requests"}
---
