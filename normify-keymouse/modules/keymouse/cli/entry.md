---
uid: 7a1f0e06
id: keymouse.cli.entry
parent: keymouse.cli
tags: [cli]
name: {zh: "入口与三条路由", en: "Entry and its three routes"}
description:
  zh: >
      `serve` 起常驻引擎、`runner ...` 当它的客户端、`flow edit` 拉起图形编辑器，其余全部丢给能力层的派发。入口项目必须是控制台子系统（OutputType=Exe）——实测写成 WinExe 后没有控制台，PowerShell 也不会等它，输出直接消失。
      
  en: >
      `serve` starts the resident engine, `runner ...` acts as its client, `flow edit` launches the graphical editor, and everything else goes to the capability layer dispatch. The entry project must be a console subsystem (OutputType=Exe): measurably, as WinExe it has no console, PowerShell does not wait for it and its output simply disappears.
      
revision: 6347fbfbc3d7af8839f5dcc9c35501ce2484bdef
updated_at: "2026-10-03T11:13:17.045Z"
fingerprint: 932bf5874d8c419c2d1c24f089630dc182c309481b348acad76e201dd477cd8d
source:
  - path: "src/KeyMouse.Cli/Program.cs"
apis:
  - protocol: rpc
    path: "keymouse serve"
    description:
      zh: >
          启动常驻 Runner。
          
      en: >
          Starts the resident Runner.
          
  - protocol: rpc
    path: "keymouse <命令>"
    description:
      zh: >
          其余命令走能力层（一次性执行）。
          
      en: >
          Everything else runs once through the capability layer.
          
deps:
  - kind: call
    to: keymouse.runner.host
    to_api: "rpc:RunnerHost.Run"
    label: {zh: "serve 就是它", en: "serve is this"}
  - kind: call
    to: keymouse.cli.runner
    label: {zh: "runner 子命令", en: "The runner subcommands"}
---
