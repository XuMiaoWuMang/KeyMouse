---
uid: 7a1f0c57
id: keymouse.tests.smoke.record
parent: keymouse.tests.smoke
tags: [test, desktop, recording]
name: {zh: "录制与回放冒烟", en: "Record and replay smoke"}
description:
  zh: >
      用 KeyMouse 自己的输入驱动录制器（钩子不过滤注入事件，所以合成会话就是真会话）：录一次点击与打字 → 检查 JSON 里有对应步骤、点击是客户区坐标、步骤带窗口上下文 → `--dry-run` 回放干净 → 手写流程真回放，回读剪贴板确认字真的打进了靶子窗口 → wait-text 两条路：屏幕上已有的文字等到（并报出连续确认次数），屏幕上没有的文字超时退出码 3 且说明读到什么。
      
  en: >
      Drives the recorder with KeyMouse input (the hooks do not filter injected events, so a synthetic session is a real one): record a click and some typing, check the JSON holds those steps with client-relative coordinates and window context, dry-run the recording clean, replay a hand-written flow for real and read the clipboard back, then walk both wait-text paths - text on screen is found with its confirmation count, text that is not times out with exit 3.
      
revision: 1b316650150f1540368ee548f7d9992ccaa3239e
updated_at: "2026-10-03T11:27:16.011Z"
fingerprint: 2ab5b1e29a47f02cf1bf9588824895c65511b838f298921b03517b68cf0c2879
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
