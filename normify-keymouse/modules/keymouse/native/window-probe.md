---
uid: ab1c2d3e
id: keymouse.native.window-probe
parent: keymouse.native
tags: [win32, diagnostics]
name: {zh: "响应探测（WM_NULL）", en: "Responsiveness probe"}
description:
  zh: >
      SendMessageTimeout(WM_NULL, SMTO_ABORTIFHUNG)：窗口在超时内回应即视为活着，并记录耗时；“没回应”与“没探测过”在数据里是两个不同状态（未探测/无响应/响应=Nms）。
      
  en: >
      SendMessageTimeout(WM_NULL, SMTO_ABORTIFHUNG) treats an answer within the timeout as alive and records how long it took. Not-probed and no-answer are distinct states in the data.
      
revision: e03d53e4c41a8a7220e83123d7f8a44a54ff8dab
updated_at: "2026-10-03T08:15:19.862Z"
fingerprint: 0b87df5cca1badf5983b75c5f73c140e8c388819cd6404f29cf2eef6978e4f42
source:
  - path: "NativeWindow.cs"
    line: 84
    end_line: 86
  - path: "NativeWindow.cs"
    line: 186
    end_line: 193
apis:
  - protocol: rpc
    path: "user32!SendMessageTimeout"
    description:
      zh: >
          带超时发消息。
          
      en: >
          Sends a message with a timeout.
          
  - protocol: rpc
    path: "NativeWindow.ResponseMs"
    description:
      zh: >
          往返耗时（null = 无响应）。
          
      en: >
          Round-trip time, null when unresponsive.
          
---
