---
uid: 0b6c7d8e
id: keymouse.input.key
parent: keymouse.input
tags: [input, keyboard]
name: {zh: "键盘按键与组合键", en: "Keys & chords"}
description:
  zh: >
      单键按下/抬起、敲击，以及组合键的顺序按下与逆序抬起；没有显式扩展键标志时用 MapVirtualKey 推断，避免方向键/小键盘被当成普通键。
      
  en: >
      Press, release and tap, plus chords pressed in order and released in reverse; when the caller does not mark an extended key, MapVirtualKey infers it so arrows and numpad are not sent as plain keys.
      
revision: d870b0c5dfd7fe29675863c0288b572a6111a8d4
updated_at: "2026-10-03T07:28:04.452Z"
fingerprint: 3618318b51206f21111b1abedb3cc5ef20caa74b67c90f6583a8fea20b8a2783
source:
  - path: "NativeInput.cs"
    line: 216
    end_line: 248
apis:
  - protocol: rpc
    path: "NativeInput.Key"
    description:
      zh: >
          单键按下或抬起。
          
      en: >
          Presses or releases one key.
          
  - protocol: rpc
    path: "NativeInput.Tap"
    description:
      zh: >
          敲击一个键。
          
      en: >
          Taps one key.
          
  - protocol: rpc
    path: "NativeInput.Chord"
    description:
      zh: >
          组合键（按序下、逆序上）。
          
      en: >
          A chord, in order and back.
          
deps:
  - kind: call
    to: keymouse.input.send
    from_api: "rpc:NativeInput.Key"
    to_api: "rpc:NativeInput.Send"
    label: {zh: "投递", en: "Dispatch"}
  - kind: call
    to: keymouse.native.user32-input
    from_api: "rpc:NativeInput.Key"
    to_api: "rpc:user32!MapVirtualKey"
    label: {zh: "扩展键推断", en: "Extended key"}
---
