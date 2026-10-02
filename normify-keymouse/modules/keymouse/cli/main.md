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
      
revision: 8d69164cfe5fa0896f595c0bca51f97977ff51b9
updated_at: "2026-10-02T17:39:02.865Z"
fingerprint: ed066abe077cf617475b89420866941ec0b7c26b595508435b3db3daac6799d5
source:
  - path: "Program.cs"
    line: 9
    end_line: 59
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
---
