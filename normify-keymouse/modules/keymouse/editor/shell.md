---
uid: 7a1f0d11
id: keymouse.editor.shell
parent: keymouse.editor
tags: [editor, winui, ui]
name: {zh: "窗口外壳与交互", en: "Shell and interactions"}
description:
  zh: >
      编辑器的窗口外壳：工具栏（打开/保存/试运行/播放/暂停/停止）、步骤列表、检查器、JSON 预览、运行输出面板与信息条。快捷键：Ctrl+O 打开、Ctrl+S 保存、**F5 播放**；命令行带上一个流程文件即可直接打开它（`KeyMouse flow edit x.json` 也走这条）。
      
  en: >
      The editor window shell: toolbar (open, save, dry run, play, pause, stop), step list, inspector, JSON preview, run output panel and info bar. Shortcuts: Ctrl+O open, Ctrl+S save, F5 play; a flow file on the command line opens straight away (so does KeyMouse flow edit x.json).
      
revision: eee5c2b2dedd9f702beb19d6a8286d336b3db1a3
updated_at: "2026-10-03T14:00:46.182Z"
fingerprint: 319beac10b0dc04a80ced5fed389341035601cc446b9b9672210a0d537b583b8
source:
  - path: "editor/KeyMouse.FlowEditor/MainWindow.xaml"
  - path: "editor/KeyMouse.FlowEditor/MainWindow.xaml.cs"
  - path: "editor/KeyMouse.FlowEditor/App.xaml.cs"
apis:
  - protocol: rpc
    path: "MainWindow.OpenFile"
    description:
      zh: >
          打开一个流程（命令行参数与"打开"按钮都走它）。
          
      en: >
          Opens a flow (both the command line and the Open button go through it).
          
  - protocol: rpc
    path: "MainWindow.RunAsync"
    description:
      zh: >
          试运行/播放：存一份给 run，输出实时进日志。
          
      en: >
          Dry run/play: hand a copy to run and stream its output into the log.
          
deps:
  - kind: call
    to: keymouse.editor.bridge
    to_api: "rpc:KeyMouseBridge.RunFlowAsync"
    label: {zh: "播放走命令行", en: "Replay through the CLI"}
  - kind: call
    to: keymouse.editor.steps
    to_api: "rpc:EditorModel.Load"
    label: {zh: "读流程", en: "Load a flow"}
---
