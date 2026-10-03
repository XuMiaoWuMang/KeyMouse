---
uid: 7a1f3001
id: keymouse.tests.smoke.focus
parent: keymouse.tests.smoke
tags: [test, desktop]
name: {zh: "聚焦与还原冒烟", en: "Focus and restore smoke"}
description:
  zh: >
      用真窗口验证 Windows 上最容易"假成功"的一件事：把靶子最小化后，① 流程没说允许就**不还原**（退出码 4，信息里告诉你加 --allow-restore，这条断言直接问 CLI 要原话，因为运行日志会截短）；② 说了允许（`"allowRestore": true`）就能还原 + 聚焦 + **把点击真的发出去**（聚焦校验通过才会发送，退出码 0）；③ 结束后窗口恢复可见且是前台。
      
  en: >
      On a real window, the thing Windows fakes most easily: minimize the target, then 1) a flow that did not ask must not restore it (exit 4, with a message saying how to allow it - asserted against the CLI own words, because run logs truncate), 2) a flow that carries "allowRestore": true restores, focuses and really sends the click (the focus gate decides that, exit 0), 3) the window ends visible and in the foreground.
      
revision: 96c306061d63c034a01c2369e4abc295251e2f35
updated_at: "2026-10-03T13:37:48.969Z"
fingerprint: 173ff3659f0b9425597d971a3505e95df46561bb0a5becd5b23cc3cb7f87b294
source:
  - path: "tests/smoke/focus.ps1"
apis:
  - protocol: rpc
    path: "聚焦与还原断言组"
    description:
      zh: >
          同意语义、还原聚焦与点击、结束后状态。
          
      en: >
          Consent, restore/focus with a click, and the state afterwards.
          
deps:
  - kind: call
    to: keymouse.window.focus
    to_api: "rpc:WindowFocus.Focus"
    label: {zh: "被测的聚焦", en: "Focus under test"}
  - kind: reference
    to: keymouse.tests.smoke.common
    to_api: "rpc:Check"
    label: {zh: "断言与靶子", en: "Assertions and target"}
---
