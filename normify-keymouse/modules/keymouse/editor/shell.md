---
uid: 7a1f0d11
id: keymouse.editor.shell
parent: keymouse.editor
tags: [editor, winui, ui]
name: {zh: "窗口外壳与交互", en: "Shell and interactions"}
description:
  zh: >
      Mica 背景 + 自定义标题栏 + 工具栏（打开/保存/另存为/添加步骤/上移下移复制删除/取区域/试运行/播放/停止）+ 状态 InfoBar 与运行日志。退出码按 CLI 的同一套含义解释给用户看；有未保存改动时关窗先问；"试运行/播放"在未保存时写临时文件，绝不悄悄覆盖正在编辑的文件。
      
  en: >
      Mica backdrop, custom title bar and toolbar (open/save/save-as/add/move/duplicate/delete/pick-region/dry-run/play/stop) plus a status InfoBar and a run log. Exit codes are explained with the same meanings the CLI documents, closing with unsaved changes asks first, and dry-run/play write a scratch file rather than silently overwriting the file being edited.
      
revision: 6347fbfbc3d7af8839f5dcc9c35501ce2484bdef
updated_at: "2026-10-03T11:13:17.047Z"
fingerprint: f750c9c3f045078536995af88a134abe107c9729da4b0166b8a3e83e8619a390
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
