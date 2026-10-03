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
      
revision: 01b8c3c91b12f7561f62aa9f6af36e7eb9e3bb94
updated_at: "2026-10-03T10:33:31.240Z"
fingerprint: 7b89223175c6951d371aee58cef8887e1b800bc48bd29e342b47ce54ffc6b589
source:
  - path: "FlowEditorCommand.cs"
apis:
  - protocol: rpc
    path: "FlowEditorCommand.Run"
    description:
      zh: >
          定位并启动图形编辑器。
          
      en: >
          Locates and launches the graphical editor.
          
---
