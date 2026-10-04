---
uid: 7a1f0d11
id: keymouse.editor.shell
parent: keymouse.editor
tags: [editor, winui, ui]
name: {zh: "窗口外壳与交互", en: "Shell and interactions"}
description:
  zh: >
      编辑器的窗口外壳：工具栏（常用步骤一排 + 「更多」、试运行 / 播放 F5 / 暂停 / 停止、文件与取区域收进「⋯」）、可拖的三栏（步骤列表 / 分隔条 / 参数检查器）与下方的运行输出面板（结论条 + 日志，没有输出时高度为零）。消息一律落在版面里，没有浮层；最小窗口 1360x960（程序化改尺寸也夹紧）；启动时焦点有意指定（有文档给步骤列表，没有就给「新建流程」）。步骤行的运行结果来自 Runner 报告的退出码与耗时，不来自界面猜测。
      
  en: >
      The window shell: a toolbar (common step types plus More, dry run / play F5 / pause / stop, file and pick-region in the overflow), three draggable panes and the run output panel below (conclusion bar plus log, zero height when empty). Messages land inside the layout - there is no overlay; the minimum window is 1360x960 even against a programmatic resize. A step row's result comes from the Runner's exit code and timing.
      
revision: ef3cf740e80bd62dd7540df6ee4c526380163233
updated_at: "2026-10-04T14:24:35.931Z"
fingerprint: eda52fae1201ded53b11691c19ff802fce452b575e05a62ba010f194334b4b97
source:
  - path: "editor/KeyMouse.FlowEditor/MainWindow.xaml"
  - path: "editor/KeyMouse.FlowEditor/MainWindow.xaml.cs"
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
          
  - protocol: rpc
    path: "MainWindow.UpdateStates"
    description:
      zh: >
          一处决定什么可见：栏位、按钮与两个空状态。
          
      en: >
          One place decides what is visible: panes, buttons and both empty states.
          
  - protocol: rpc
    path: "MainWindow.AddStep"
    description:
      zh: >
          在选中那一步之后插入一步，并选中它。
          
      en: >
          Inserts a step after the selected one and selects it.
          
deps:
  - kind: call
    to: keymouse.editor.bridge
    to_api: "rpc:KeyMouseBridge.RunFlowAsync"
    label: {zh: "播放走命令行", en: "Replay through the CLI"}
  - kind: call
    to: keymouse.editor.steps
    to_api: "rpc:EditorModel.Load"
    label: {zh: "读流程", en: "Load a flow"}
  - kind: call
    to: keymouse.editor.inspector
    from_api: "rpc:MainWindow.UpdateStates"
    to_api: "rpc:InspectorBuilder.Build"
    label: {zh: "重建参数卡", en: "Rebuilds the form"}
---
