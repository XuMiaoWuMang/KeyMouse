---
uid: c1d2e3f4
id: keymouse.cli.main
parent: keymouse.cli
tags: [cli]
name: {zh: "主入口与命令组派发", en: "Main entry & group dispatch"}
description:
  zh: >
      Main 的全部工作：把 CommandFailure 与参数异常映射成退出码（2 = 用法），按首个参数选择命令组并转发剩余参数，空参数时打印帮助。命令组里有一处值得记下来：schema 打印流程格式的契约本身（不带参数打全部，带一个类型就只看那一种）——前端照着它渲染，人也可以直接读。
      
  en: >
      Everything Main does: map CommandFailure and argument errors onto exit codes (2 = usage), pick a command group by the first argument and forward the rest, print help when nothing is given. One group is worth noting: schema prints the flow format contract itself (all of it, or one step type) - the front end renders from it and a human can read it.
      
revision: ef3cf740e80bd62dd7540df6ee4c526380163233
updated_at: "2026-10-04T14:24:35.933Z"
fingerprint: c2466d777f9e13ef4b2681d5d98fa1383d4339672611d90ef8fad5764cbbc7fc
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
          
  - protocol: rpc
    path: "keymouse schema"
    description:
      zh: >
          打印流程格式契约（全部，或只看一种类型）。
          
      en: >
          Prints the flow format contract (all of it, or one step type).
          
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
  - kind: call
    to: keymouse.flow.schema
    from_api: "rpc:keymouse schema"
    to_api: "rpc:FlowSchema.Steps"
    label: {zh: "打印契约", en: "Prints the contract"}
---
