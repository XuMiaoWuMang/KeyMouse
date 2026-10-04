---
uid: 7a1f0e02
id: keymouse.runner.protocol
parent: keymouse.runner
tags: [runner, protocol]
name: {zh: "管道协议", en: "Pipe protocol"}
description:
  zh: >
      一行一个 JSON 对象，UTF-8，走命名管道：请求是 {id, method, params}，事件是 {kind, id, ...}。刻意无聊——任何语言十行能接、人能直接读，没有端口也没有序列化框架。四块：契约文件（唯一真相）、版本协商（先对版本再谈方法）、方法表（12 个方法各自的参数、流式事件与终结事件）、事件表（6 种 kind，以及那条不是应答的问候）。
      
  en: >
      One JSON object per line, UTF-8, over a named pipe: a request is {id, method, params}, an event is {kind, id, ...}. Deliberately boring - ten lines in any language, readable by a human, no ports, no serialization framework. Four parts: the contract file (single source of truth), version negotiation, the method table (12 methods, each with a terminal event) and the event table (6 kinds plus the greeting that is not a reply).
      
revision: ef3cf740e80bd62dd7540df6ee4c526380163233
updated_at: "2026-10-04T14:24:35.930Z"
fingerprint: 57e2a9866514486ce02dab49e76e8c9be7b4760bb588773d44e29f49204d41b8
source:
  - path: "src/KeyMouse.Runner/RunnerProtocol.cs"
---
