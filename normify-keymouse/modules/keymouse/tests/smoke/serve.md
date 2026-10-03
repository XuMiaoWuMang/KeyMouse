---
uid: 7a1f1103
id: keymouse.tests.smoke.serve
parent: keymouse.tests.smoke
tags: [test, desktop, runner]
name: {zh: "常驻 Runner 冒烟", en: "Resident Runner smoke"}
description:
  zh: >
      把架构本身当作被测对象：起 `serve`、用 CLI 当客户端问状态（版本与管道名）、让常驻 Runner 执行一个流程并逐步看到事件、作业表里出现它的退出码、最后 `runner stop` 让它退出。
      
  en: >
      The architecture itself under test: start serve, ask it for status from the CLI (version and pipe name), have the resident Runner execute a flow and watch each step reported, see the finished job and its exit code in the job table, then stop it.
      
revision: bc06440df10935e9e8479ecad7b52098b48df7fd
updated_at: "2026-10-03T12:19:12.568Z"
fingerprint: ed41d53007d720d04e378da261d777526dbeb45c6684385d6beeb88738b99d95
source:
  - path: "tests/smoke/serve.ps1"
apis:
  - protocol: rpc
    path: "常驻 Runner 断言组"
    description:
      zh: >
          serve/status/run/stop 全链路。
          
      en: >
          The serve/status/run/stop chain.
          
---
