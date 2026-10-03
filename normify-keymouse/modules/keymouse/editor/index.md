---
uid: 7a1f0d10
id: keymouse.editor
parent: keymouse
tags: [editor, winui]
name: {zh: "图形编辑器", en: "Graphical editor"}
description:
  zh: >
      WinUI 3 的无包自包含应用（editor/KeyMouse.FlowEditor），只做命令行做不好的那一件事——编辑流程。它把 keymouse-flow 的 schema 原样链接进来，取区域与播放都调命令行自身，因此"编辑器写出来的东西 run 认不认"不靠自觉。部署上必须自包含：本机只有 Windows App Runtime 的 CBS 版本，无包引导程序找不到可用运行时（启动即 0x80670016）。
      
  en: >
      An unpackaged, self-contained WinUI 3 app (editor/KeyMouse.FlowEditor) that does the one thing the command line cannot: editing a flow. It links the keymouse-flow schema verbatim and asks the command line itself for region picking and replay, so "does run accept what the editor wrote" is not a matter of discipline. Self-contained is required here: only the CBS runtime exists, and the unpackaged bootstrapper finds nothing (0x80670016).
      
revision: 6347fbfbc3d7af8839f5dcc9c35501ce2484bdef
updated_at: "2026-10-03T11:13:17.047Z"
fingerprint: 55033d25fce13c9d5c3b44610d32254427cbc7105f8b075656fa39d74ca6dbdc
source:
  - path: "editor/KeyMouse.FlowEditor/KeyMouse.FlowEditor.csproj"
---
