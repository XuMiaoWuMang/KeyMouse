---
uid: 7a1f100f
id: keymouse.runner.protocol.contract
parent: keymouse.runner.protocol
tags: [runner, protocol, contract]
name: {zh: "协议契约文件", en: "Protocol contract file"}
description:
  zh: >
      协议的规范本体：12 个方法与各自的终结事件、6 种事件与字段、16 个参数、错误码（与 CLI 退出码同源）、不变量、连接后的问候、result 里的具名形状，以及 knownGaps。knownGaps 现在必须是空的：发现差异先写进去、再把那条差异的行为钉进测试，缺口要么被修掉要么被写下来。改协议的顺序是固定的：先改这里 → 再改两侧 → 跑 `--only protocol`（测试读它，反过来不成立）。
      
  en: >
      The normative artifact: 12 methods with their terminal events, 6 event kinds and fields, 16 parameters, error codes (same set as the CLI exit codes), invariants, the connect-time greeting, result shapes, and knownGaps. knownGaps must stay empty: a difference is written down and pinned by a test - a gap is either fixed or recorded. Change order is fixed: this file, then both sides, then --only protocol (the test reads this file, never the other way round).
      
revision: ef3cf740e80bd62dd7540df6ee4c526380163233
updated_at: "2026-10-04T14:24:35.925Z"
fingerprint: 11bf030bb413ad49f19ea68677c16efa8e7092848da0bfc6bccf564fa5280e42
source:
  - path: "src/KeyMouse.Runner/runner-protocol.schema.json"
apis:
  - protocol: file
    path: "src/KeyMouse.Runner/runner-protocol.schema.json"
    description:
      zh: >
          契约本体（机器可读的那份）。
          
      en: >
          The contract itself (the machine-readable one).
          
  - protocol: rpc
    path: "knownGaps"
    description:
      zh: >
          已知差异清单：必须为空，测试盯着它。
          
      en: >
          The known-gaps list: must be empty, and a test watches it.
          
---
