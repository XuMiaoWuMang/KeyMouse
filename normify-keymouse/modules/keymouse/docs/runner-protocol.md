---
uid: 7a1f100d
id: keymouse.docs.runner-protocol
parent: keymouse.docs
tags: [docs, protocol]
name: {zh: "协议规范文档", en: "Protocol document"}
description:
  zh: >
      机器可读契约旁边那份规范文档：谁跟谁说话、不可协商的传输、请求/事件模型、方法表（12）、事件表（6）、16 个字段的参数袋、与 CLI 退出码同源的错误码、一致性检查结果，以及 2026-10-04 收口的两条缺口的留档（hello 现在与别的方法同一套规则；client/version 真的用起来了）。
      
  en: >
      The normative document beside the machine-readable contract: who talks to whom, the transport that is not negotiable, the request/event model, the method table (12), the event table (6), the 16-field parameter bag, the error codes shared with the CLI exit codes, the consistency-check results, and the archive of the two gaps closed on 2026-10-04 (hello now follows the same rules; client/version are really used).
      
revision: ef3cf740e80bd62dd7540df6ee4c526380163233
updated_at: "2026-10-04T14:24:35.928Z"
fingerprint: ab8b9709c660cca96bfe0114a4c41bbf7c70642d72968693a72a61eefe9512dc
source:
  - path: "docs/runner-protocol.md"
apis:
  - protocol: file
    path: "docs/runner-protocol.md"
    description:
      zh: >
          管道协议的规范文档：传输、方法、事件、参数、错误码。
          
      en: >
          The normative document for the pipe protocol: transport, methods, events, parameters, error codes.
          
deps:
  - kind: reference
    to: keymouse.runner.protocol.contract
    to_api: "file:src/KeyMouse.Runner/runner-protocol.schema.json"
    label: {zh: "写的是那份契约", en: "Documents the contract"}
---
