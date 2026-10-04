---
uid: 7a1f1004
id: keymouse.runner.protocol.methods
parent: keymouse.runner.protocol
tags: [runner, protocol, methods]
name: {zh: "方法表与派发", en: "Method table and dispatch"}
description:
  zh: >
      12 个方法，每一个都以终结事件收场（result / finished / error）——这条不变量是从一次真实缺口里长出来的：hello 曾经不回显请求 id、也不发终结事件，`await hello` 会永远不返回。现在 hello 与别的方法同一套规则：回显 id、以 result{version, protocol} 终结。执行类方法先流式回 log 与 step，再以 finished（带退出码）或 error 收尾；不认识的方法必须回 error(code=2)，message 里列出的可用方法由 RunnerProtocol.Methods 生成，与契约同源。
      
  en: >
      Twelve methods, each ending in a terminal event (result / finished / error) - an invariant that grew from a real gap: hello used to not echo the request id and to send no terminal event, so awaiting it never returned. Executing methods stream log and step, then finish with finished or error; an unknown method answers error(code=2) listing the available methods, generated from RunnerProtocol.Methods so it cannot drift.
      
revision: ef3cf740e80bd62dd7540df6ee4c526380163233
updated_at: "2026-10-04T14:24:35.926Z"
fingerprint: 2027cf018ce3e9c73d08e1d3799ce905591950edad60e12dcd6c4cb024722bf4
source:
  - path: "src/KeyMouse.Runner/RunnerProtocol.cs"
  - path: "src/KeyMouse.Runner/RunnerHost.cs"
apis:
  - protocol: rpc
    path: "keymouse-runner 请求"
    description:
      zh: >
          {id, method, params}：hello | status | list | run | record | validate | pick-region | ocr | cancel | pause | resume | shutdown。
          
      en: >
          Request {id, method, params}: hello | status | list | run | record | validate | pick-region | ocr | cancel | pause | resume | shutdown.
          
  - protocol: rpc
    path: "RunnerProtocol.Methods"
    description:
      zh: >
          方法清单的唯一来源：服务端那句"不认识这个方法"由它生成，测试拿它与契约对账。
          
      en: >
          The single source of the method list: the unknown-method message comes from it, a test reconciles it with the contract.
          
  - protocol: rpc
    path: "hello"
    description:
      zh: >
          回显请求 id，以 result{version, protocol} 终结。
          
      en: >
          Echoes the request id and terminates with result{version, protocol}.
          
  - protocol: rpc
    path: "run / record"
    description:
      zh: >
          执行类方法：流式回 log 与 step，终结 finished 或 error。
          
      en: >
          Executing methods: stream log and step, terminate with finished or error.
          
  - protocol: rpc
    path: "cancel / pause / resume"
    description:
      zh: >
          控制正在跑的作业；target 缺省时取请求 id 自身。
          
      en: >
          Control a running job; target defaults to the request's own id.
          
deps:
  - kind: call
    to: keymouse.runner.host
    from_api: "rpc:keymouse-runner 请求"
    label: {zh: "按方法名派发", en: "Dispatch by method name"}
  - kind: call
    to: keymouse.flow.runner
    from_api: "rpc:run / record"
    label: {zh: "执行走能力层", en: "Through the capability layer"}
---
