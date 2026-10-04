---
uid: 7a1f100b
id: keymouse.editor.app
parent: keymouse.editor
tags: [editor, theme, crash]
name: {zh: "应用、设计令牌与崩溃兜底", en: "App, tokens and crash guard"}
description:
  zh: >
      应用对象与它装上的那套长相：App.xaml 是设计令牌（字体四级、圆角两档、卡片/面板/工具按钮样式）与四条改之前先读的规矩，其中有"没有浮层"与"自绘面零投影"；App.xaml.cs 负责开窗、命令行给了一个流程文件就直接打开它，并把所有未处理异常写进 %LOCALAPPDATA%\KeyMouse\editor-crash.log 且 Handled = true——否则一个抛异常的点击就只是把进程弄死，什么线索都不留。
      
  en: >
      The application object and the look it installs: App.xaml carries the design tokens (four type levels, two radii, card/panel/tool-button styles) and the four rules a change must respect, including 'no overlays' and 'no shadows'. App.xaml.cs opens the window, opens a flow straight away when one is on the command line, and catches every unhandled exception into %LOCALAPPDATA%\KeyMouse\editor-crash.log with Handled = true - otherwise a click that throws simply kills the process and leaves no clue.
      
revision: ef3cf740e80bd62dd7540df6ee4c526380163233
updated_at: "2026-10-04T14:24:35.927Z"
fingerprint: 2ff45217eac3f18e590dd118c1cf0ea2a711052cfa0b68d3f89d878875c041d1
source:
  - path: "editor/KeyMouse.FlowEditor/App.xaml"
  - path: "editor/KeyMouse.FlowEditor/App.xaml.cs"
apis:
  - protocol: file
    path: "editor/KeyMouse.FlowEditor/App.xaml"
    description:
      zh: >
          这套设计系统的令牌：字体四级、圆角两档、卡片与工具按钮样式。
          
      en: >
          The design tokens: four type levels, two radii, card and tool-button styles.
          
  - protocol: rpc
    path: "App.OnUnhandled"
    description:
      zh: >
          把每个未处理异常写进 editor-crash.log，并让进程活下来。
          
      en: >
          Writes every unhandled exception to editor-crash.log and keeps the process alive.
          
deps:
  - kind: call
    to: keymouse.editor.shell
    to_api: "rpc:MainWindow.OpenFile"
    label: {zh: "开窗", en: "Opens the window"}
---
