---
uid: 7a1f1003
id: keymouse.runner.protocol.version
parent: keymouse.runner.protocol
tags: [runner, protocol, version]
name: {zh: "版本协商", en: "Version negotiation"}
description:
  zh: >
      契约版本只有一处定义（RunnerProtocol.ContractVersion，测试拿它与 schema 的 version 对账）。客户端在每个请求上自报家门：client 说明谁在问（取入口程序集名，服务端记进作业，list/status 看得到），version 说明说的是哪版协议。服务端先对版本再谈方法：主版本不合就回 error(code=2) 并停止处理，而不是拿旧字段跑出看不懂的结果；没带 version 视为不说、放行，所以脚本随手发一行也能用。
      
  en: >
      The contract version has one definition (RunnerProtocol.ContractVersion, reconciled with the schema version by a test). Every request declares itself: client says who is asking (entry assembly name; the server records it on the job so list/status show it), version says which protocol it speaks. The server agrees on the version before methods: a different major answers error(code=2) and stops; an absent version passes, so a hand-written script line still works.
      
revision: ef3cf740e80bd62dd7540df6ee4c526380163233
updated_at: "2026-10-04T14:24:35.926Z"
fingerprint: 57e2a9866514486ce02dab49e76e8c9be7b4760bb588773d44e29f49204d41b8
source:
  - path: "src/KeyMouse.Runner/RunnerProtocol.cs"
apis:
  - protocol: rpc
    path: "RunnerProtocol.ContractVersion"
    description:
      zh: >
          契约版本（与 schema 的 version 相等，由测试强制）。
          
      en: >
          The contract version (equal to the schema version, enforced by a test).
          
  - protocol: rpc
    path: "RunnerProtocol.AcceptsVersion"
    description:
      zh: >
          客户端报的版本能不能用：没报放行，主版本不合挡下。
          
      en: >
          Whether the client's declared version is usable: absent passes, a different major is refused.
          
---
