---
uid: 7a1f0d15
id: keymouse.cli.flow
parent: keymouse.cli
tags: [cli, editor]
name: {zh: "flow edit 入口", en: "flow edit entry"}
description:
  zh: >
      `KeyMouse flow edit <流程.json>`：先找编辑器（KeyMouse.exe 旁边 → 开发目录里的构建输出），找到就带着流程路径启动它，找不到就报清楚该构建什么（退出码 4）。控制台不自己渲染任何界面。
      
  en: >
      `KeyMouse flow edit <flow.json>`: looks for the editor (next to KeyMouse.exe, then the build output of a development checkout), starts it with the flow path, and when it is nowhere says exactly what to build (exit code 4). The console renders no UI itself.
      
revision: ef3cf740e80bd62dd7540df6ee4c526380163233
updated_at: "2026-10-04T14:24:35.933Z"
fingerprint: 233349297782e8222f440c69c66b04a5f4b364f938edc9e43a73b7804b813605
source:
  - path: "src/KeyMouse.Cli/FlowEditorCommand.cs"
apis:
  - protocol: rpc
    path: "FlowEditorCommand.Run"
    description:
      zh: >
          定位并启动图形编辑器。
          
      en: >
          Locates and launches the graphical editor.
          
---
