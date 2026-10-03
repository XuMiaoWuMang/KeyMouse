---
uid: 7a1f0d13
id: keymouse.editor.bridge
parent: keymouse.editor
tags: [editor, cli]
name: {zh: "命令行桥接", en: "Command-line bridge"}
description:
  zh: >
      找 KeyMouse.exe（编辑器旁边 → 向上找 dist → PATH）、调 `region pick --json` 并把结果解析成"坐标 + 窗口"、调 `run` 并逐行流出输出。进程参数一个一个传（不过 shell），所以带空格的进程名安全；被取消时连同子进程一起杀掉。
      
  en: >
      Finds KeyMouse.exe (next to the editor, then up the tree for a dist folder, then PATH), calls `region pick --json` and parses it into coordinates plus the window, and calls `run` while streaming its output. Arguments are passed one by one (never through a shell), so process names with spaces are safe, and cancelling kills the child tree.
      
revision: 6347fbfbc3d7af8839f5dcc9c35501ce2484bdef
updated_at: "2026-10-03T11:13:17.047Z"
fingerprint: dcd1c57df3f0ab957e6aacf3455041ace8ab7950e776109fed68dfc2d533437d
source:
  - path: "editor/KeyMouse.FlowEditor/KeyMouseBridge.cs"
apis:
  - protocol: file
    path: "KeyMouse.exe"
    description:
      zh: >
          编辑器调用命令行工具的唯一入口（找不到时界面会说明去哪构建）。
          
      en: >
          The only way the editor reaches the tool; when it is missing the UI says what to build.
          
  - protocol: rpc
    path: "KeyMouseBridge.PickRegionAsync"
    description:
      zh: >
          拉选区浮层并把结果填进当前步骤。
          
      en: >
          Pulls up the region overlay and feeds the result into the current step.
          
  - protocol: rpc
    path: "KeyMouseBridge.RunFlowAsync"
    description:
      zh: >
          播放/试运行并把输出交给界面。
          
      en: >
          Replays or dry-runs and hands the output to the UI.
          
deps:
  - kind: call
    to: keymouse.pick.overlay
    label: {zh: "取区域就是那个浮层", en: "Region picking is that overlay"}
  - kind: call
    to: keymouse.flow.runner
    to_api: "rpc:FlowRunner.Run"
    label: {zh: "播放也是它", en: "And replay is that runner"}
---
