---
uid: 7a1f0c52
id: keymouse.native.hooks
parent: keymouse.native
tags: [native, recording, hooks]
name: {zh: "全局键鼠钩子", en: "Global input hooks"}
description:
  zh: >
      WH_KEYBOARD_LL / WH_MOUSE_LL：装钩子、把事件原样传给下一个钩子（不吞输入）、用一个 PeekMessage 轮询泵维持钩子线程——低级钩子必须有消息泵，而录制还要在没有任何输入时注意到自己的停止条件。事件带钩子自己的时间戳，录制出来的停顿才是用户真实留下的。
      
  en: >
      WH_KEYBOARD_LL / WH_MOUSE_LL: install, pass every event through untouched (never swallow input), and keep the hook thread alive with a PeekMessage poll - low-level hooks need a pumping thread, and a recorder also has to notice its stop conditions while no input arrives. Events carry the hook timestamps, so the recorded pauses are the ones the user really left.
      
revision: 6347fbfbc3d7af8839f5dcc9c35501ce2484bdef
updated_at: "2026-10-03T11:11:56.203Z"
fingerprint: e19d0385e3e8bbd557aec002434fb3d2b8a47a69b6c532ec90ced03a5c6fd95c
source:
  - path: "src/KeyMouse.Core/NativeHooks.cs"
apis:
  - protocol: rpc
    path: "NativeHooks.Record"
    description:
      zh: >
          装两个低级钩子并泵消息直到停止。
          
      en: >
          Installs both low-level hooks and pumps messages until stopped.
          
  - protocol: rpc
    path: "NativeHooks.ToUnicodeEx"
    description:
      zh: >
          按当前布局把按键翻成字符。
          
      en: >
          Turns a key into its character under the active layout.
          
---
