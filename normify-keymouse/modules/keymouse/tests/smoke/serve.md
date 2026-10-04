---
uid: 7a1f1103
id: keymouse.tests.smoke.serve
parent: keymouse.tests.smoke
tags: [test, desktop, runner]
name: {zh: "常驻 Runner 冒烟", en: "Resident Runner smoke"}
description:
  zh: >
      常驻 Runner 冒烟：用 CLI 当客户端问状态（版本与管道名）、让常驻 Runner 执行一个流程并逐步看到事件、作业表里出现退出码、最后停掉它。**前提是这条管道上没有别的 Runner**：编辑器自己就会起一个，抢管道既会造成假失败，也会在收尾时把用户的 Runner 停掉。所以模块先检查，发现别人在听就明确说明并退回（剩下的模块照跑）。
      
  en: >
      The resident Runner under test: ask it for status from the CLI (version and pipe name), have it execute a flow and watch each step, see the finished job with its exit code, then stop it. The precondition is that no other Runner owns this pipe - the editor starts one, and fighting over it both causes false failures and stops the user runner at teardown. So the module checks first and, finding one, says so plainly and steps aside.
      
revision: ef3cf740e80bd62dd7540df6ee4c526380163233
updated_at: "2026-10-04T14:23:38.098Z"
fingerprint: 962091f68db4a862c363b527f3137b0f611266b4f322774a5c648255e5fda709
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
