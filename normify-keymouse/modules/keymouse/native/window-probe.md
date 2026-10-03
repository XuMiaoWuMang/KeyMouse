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
      
revision: 6347fbfbc3d7af8839f5dcc9c35501ce2484bdef
updated_at: "2026-10-03T11:11:56.204Z"
fingerprint: d39f953f3e29de7157808217fe297fca70c2373b4379b06e762d25bbb047f540
source:
  - path: "src/KeyMouse.Core/NativeWindow.cs"
    line: 84
    end_line: 86
  - path: "src/KeyMouse.Core/NativeWindow.cs"
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
