---
uid: f4a5b6c7
id: keymouse.cli.int-arg
parent: keymouse.cli
tags: [cli, parsing]
name: {zh: "整数参数解析", en: "Integer argument parsing"}
description:
  zh: >
      统一的整数解析与默认值取值：不合法的整数会带上「参数是什么」的说明报错（如「'abc' 不是合法的整数（--pick）」），避免只报一个光秃的转换失败。
      
  en: >
      One place for integer parsing and defaults; an invalid integer reports what the argument was for instead of a bare conversion failure.
      
revision: 6347fbfbc3d7af8839f5dcc9c35501ce2484bdef
updated_at: "2026-10-03T11:11:56.195Z"
fingerprint: 7f2ba5c7c2569f29daf25ca2416e5988b42986f24461e2467265c7fae3dc41e9
source:
  - path: "src/KeyMouse.Core/Commands.cs"
    line: 640
    end_line: 650
apis:
  - protocol: rpc
    path: "IntArg"
    description:
      zh: >
          解析必需的整数字参。
          
      en: >
          Parses a required integer argument.
          
  - protocol: rpc
    path: "IntOr"
    description:
      zh: >
          可选整数（缺省时取默认值）。
          
      en: >
          Optional integer with a fallback.
          
---
