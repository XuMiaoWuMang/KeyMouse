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
      
revision: 6347fbfbc3d7af8839f5dcc9c35501ce2484bdef
updated_at: "2026-10-03T11:13:17.046Z"
fingerprint: df5e3927e12b6e5f13dc51371d927d1a737a674d2da50153b18902f760f91254
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
