---
uid: c1d2e3f4
id: keymouse.cli.main
parent: keymouse.cli
tags: [cli]
name: {zh: "主入口与命令组派发", en: "Main entry & group dispatch"}
description:
  zh: >
      Main 的全部工作：把 CommandFailure 与参数异常映射成退出码（2 = 用法），按首个参数选择命令组并转发剩余参数，空参数时打印帮助。
      
  en: >
      Everything Main does: map CommandFailure and argument errors onto exit codes (2 = usage), pick a command group by the first argument and forward the rest, print help when nothing is given.
      
revision: 6347fbfbc3d7af8839f5dcc9c35501ce2484bdef
updated_at: "2026-10-03T11:13:17.046Z"
fingerprint: 7f2ba5c7c2569f29daf25ca2416e5988b42986f24461e2467265c7fae3dc41e9
source:
  - path: "src/KeyMouse.Core/Commands.cs"
    line: 9
    end_line: 63
apis:
  - protocol: rpc
    path: "keymouse <命令组>"
    description:
      zh: >
          按命令组分发一次调用。
          
      en: >
          Dispatches one invocation to a command group.
          
  - protocol: rpc
    path: "keymouse --help"
    description:
      zh: >
          打印内置帮助并退出 0。
          
      en: >
          Prints built-in help and exits 0.
          
  - protocol: rpc
    path: "keymouse --version"
    description:
      zh: >
          打印版本号。
          
      en: >
          Prints the version.
          
deps:
  - kind: call
    to: keymouse.cli.options
    from_api: "rpc:keymouse <命令组>"
    to_api: "rpc:ExtractGlobalOptions"
    label: {zh: "先提取全局选项", en: "Extract options first"}
  - kind: call
    to: keymouse.cli.help
    from_api: "rpc:keymouse --help"
    to_api: "rpc:Usage.Print"
    label: {zh: "无参数时", en: "When called bare"}
  - kind: call
    to: keymouse.cli.failure
    from_api: "rpc:keymouse <命令组>"
    to_api: "rpc:CommandFailure"
    label: {zh: "异常→退出码", en: "Exception to code"}
  - kind: call
    to: keymouse.probe
    from_api: "rpc:keymouse <命令组>"
    label: {zh: "probe 组交给感知出口", en: "Hand probe to perception"}
  - kind: call
    to: keymouse.pick.placement
    to_api: "rpc:RegionCommand.Run"
    label: {zh: "派发 region 组", en: "Dispatch the region group"}
---
