---
uid: 9f4bafb0
id: keymouse.tests.docs-link
parent: keymouse.tests
tags: [test, docs]
name: {zh: "文档链接检查", en: "Docs link check"}
description:
  zh: >
      扫描全部 markdown 的相对链接是否真实存在：文档拆成四份后，改个文件名就没人重读过那些链接。不挑平台、不需要桌面，所以进了 CI。
      
  en: >
      Checks that every relative markdown link resolves: once the docs split into four files, a rename leaves links nobody re-reads. Platform-agnostic and desktop-free, so CI runs it.
      
revision: d870b0c5dfd7fe29675863c0288b572a6111a8d4
updated_at: "2026-10-03T07:28:04.462Z"
fingerprint: 8d9330ee2f80dd385ef7021009a9891fe84f726f81a10d3c2c540c8283dc4c6d
source:
  - path: "tests/check-docs.ps1"
    line: 1
    end_line: 44
apis:
  - protocol: rpc
    path: "pwsh tests/check-docs.ps1"
    description:
      zh: >
          校验全部相对链接（坏链退回非零）。
          
      en: >
          Validates every relative link, exiting non-zero on a broken one.
          
---
