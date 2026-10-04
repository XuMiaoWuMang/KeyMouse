---
uid: 7a1f1006
id: keymouse.tests.unit.protocol
parent: keymouse.tests.unit
tags: [tests, protocol, contract]
name: {zh: "协议一致性检查", en: "Protocol consistency tests"}
description:
  zh: >
      前后端通信契约的一致性检查：契约方法 = 后端实现的方法、前端调用的方法 ⊆ 契约、事件种类 ⊆ 契约、每个请求都声明了终结事件、终结事件只有 result/finished/error、参数袋两侧一致、事件字段都有定义、问候不是应答且连上就发、client/version 两侧真的在用；再加实机行为断言：hello 回 result 且回显 id、版本不合被挡下、作业记下是谁在问。
      
  en: >
      Contract consistency check between client and server: contract methods = implemented methods, front-end calls are a subset, event kinds are declared, every request declares a terminal event, only result/finished/error terminate, the parameter bag matches, event fields are defined, the greeting is no reply but is sent on connect, client/version are used - plus live behaviour: hello returns result with the echoed id, a bad version is refused, a job records the client.
      
revision: ef3cf740e80bd62dd7540df6ee4c526380163233
updated_at: "2026-10-04T14:24:35.928Z"
fingerprint: f3e18b7f23b64403e0c8001160147370bf2eb2e5e4a7a921a03e1ac43c2fe98a
source:
  - path: "tests/KeyMouse.Tests/RunnerProtocolTests.cs"
apis:
  - protocol: rpc
    path: "RunnerProtocolTests.Run"
    description:
      zh: >
          跑完整套契约检查：27 条断言。
          
      en: >
          Runs the whole contract check: 27 assertions.
          
  - protocol: rpc
    path: "--only protocol"
    description:
      zh: >
          只跑这一组（改协议时用）。
          
      en: >
          Runs only this group (fast during protocol work).
          
deps:
  - kind: reference
    to: keymouse.runner.protocol.contract
    to_api: "rpc:knownGaps"
    label: {zh: "读契约", en: "Reads the contract"}
  - kind: reference
    to: keymouse.runner.protocol.methods
    to_api: "rpc:RunnerProtocol.Methods"
    label: {zh: "对账方法表", en: "Checks the method table"}
---
