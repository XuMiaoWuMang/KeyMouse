---
uid: 7a1f0c57
id: keymouse.tests.smoke.record
parent: keymouse.tests.smoke
tags: [test, desktop, recording]
name: {zh: "录制与回放冒烟", en: "Record and replay smoke"}
description:
  zh: >
      用 KeyMouse 自己的输入驱动录制器（钩子不过滤注入事件，所以合成会话就是真会话）：录一次点击与打字 → 检查 JSON 里有对应步骤、点击是客户区坐标、步骤带窗口上下文 → 原样 `--dry-run` 回放必须干净 → 再拿一份手写流程真回放，回读剪贴板确认字真的打进了靶子窗口。
      
  en: >
      Drives the recorder with KeyMouse input (the hooks do not filter injected events, so a synthetic session is a real one): record a click and some typing, check the JSON holds those steps with client-relative coordinates and window context, dry-run the recording clean, then replay a hand-written flow for real and read the clipboard back to prove the text landed in the target window.
      
revision: 8138caf9efa304f3487fcf5c6320ee4ac39f036b
updated_at: "2026-10-03T10:12:03.455Z"
fingerprint: eadaa108ac9f863522a90e9ed23758b50b503022f84b73063f2608ffa340dc2c
source:
  - path: "tests/smoke.ps1"
    line: 523
    end_line: 568
apis:
  - protocol: rpc
    path: "录制与回放断言组"
    description:
      zh: >
          录制产物内容 + 回放真的生效。
          
      en: >
          What the recording holds, and that replay really works.
          
deps:
  - kind: call
    to: keymouse.record.session
    to_api: "rpc:Recorder.Run"
    label: {zh: "启动录制", en: "Start the recorder"}
  - kind: call
    to: keymouse.flow.runner
    to_api: "rpc:FlowRunner.Run"
    label: {zh: "回放流程", en: "Replay the flow"}
---
