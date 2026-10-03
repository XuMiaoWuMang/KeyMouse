---
uid: 7a1f0e02
id: keymouse.runner.protocol
parent: keymouse.runner
tags: [runner, protocol]
name: {zh: "管道协议", en: "Pipe protocol"}
description:
  zh: >
      一行一个 JSON 对象：请求是 `method` + `params`，事件是 `kind`（hello | log | step | finished | result | error）。选命名管道而不是端口：按用户隔离（`keymouse-runner-<用户名>`）、无依赖、任何语言十行能接、人能直接读。请求并发处理，作业串行执行——否则客户端在长流程期间连 cancel 都发不进来。
      
  en: >
      One JSON object per line: a request is `method` plus `params`, an event is a `kind` (hello | log | step | finished | result | error). A named pipe rather than a port: per-user isolation (keymouse-runner-<user>), no dependency, ten lines in any language, and a human can read the traffic. Requests are handled concurrently while jobs run one at a time - otherwise a client could not even send cancel during a long flow.
      
revision: 6347fbfbc3d7af8839f5dcc9c35501ce2484bdef
updated_at: "2026-10-03T11:13:17.044Z"
fingerprint: cd5effed268b21e575132d97dcdd66949c10d77cd02a31e5275dfa2b9a3fb7f7
source:
  - path: "src/KeyMouse.Runner/RunnerProtocol.cs"
apis:
  - protocol: rpc
    path: "keymouse-runner 请求"
    description:
      zh: >
          {id, method, params}：hello | status | list | run | record | validate | pick-region | ocr | cancel | pause | resume | shutdown。
          
      en: >
          Request {id, method, params}: hello | status | list | run | record | validate | pick-region | ocr | cancel | pause | resume | shutdown.
          
  - protocol: rpc
    path: "keymouse-runner 事件"
    description:
      zh: >
          {kind, id, ...}：hello | log | step | finished | result | error。
          
      en: >
          Event {kind, id, ...}: hello | log | step | finished | result | error.
          
---
