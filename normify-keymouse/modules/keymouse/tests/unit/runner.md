---
uid: 7a1f0e09
id: keymouse.tests.unit.runner
parent: keymouse.tests.unit
tags: [test, unit, runner]
name: {zh: "Runner 协议单测", en: "Runner protocol tests"}
description:
  zh: >
      不需要桌面的那一半：请求/事件形状、未知方法给退出码 2、validate 报步数、dry-run 逐步上报且日志流回来、真实（非 dry）运行被取消后退出码必须是 7，以及**真命名管道**上的往返（用私有管道名，绝不碰用户正开着的引擎）。
      
  en: >
      The half that needs no desktop: request/event shapes, an unknown method answering with exit code 2, validate reporting the step count, a dry run reporting every step with the log stream coming back, a real (not dry) run cancelled ending in exit code 7, and a round trip over a real named pipe (private pipe name, never the engine the user has open).
      
revision: 6347fbfbc3d7af8839f5dcc9c35501ce2484bdef
updated_at: "2026-10-03T11:13:17.046Z"
fingerprint: 3b6849977e17b378a42ed222fb2f82a9aba5b483929cb6cecc6ab9002a502a0b
source:
  - path: "tests/KeyMouse.Tests/RunnerTests.cs"
apis:
  - protocol: rpc
    path: "Runner 协议与作业控制断言组"
    description:
      zh: >
          请求/事件、取消语义、管道往返。
          
      en: >
          Requests/events, cancel semantics, pipe round trips.
          
deps:
  - kind: reference
    to: keymouse.runner.host
    to_api: "rpc:RunnerHost.HandleAsync"
    label: {zh: "被测的作业派发", en: "The dispatch under test"}
  - kind: reference
    to: keymouse.runner.pipe
    to_api: "rpc:RunnerClient.SendAsync"
    label: {zh: "被测的客户端", en: "The client under test"}
---
