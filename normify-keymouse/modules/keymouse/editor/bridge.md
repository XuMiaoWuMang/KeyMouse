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
      
revision: 01b8c3c91b12f7561f62aa9f6af36e7eb9e3bb94
updated_at: "2026-10-03T10:33:31.240Z"
fingerprint: 015005b8d11bc3d5c78598d0ad2ab1d86515dd31d07d3b3e308edf14297b84c4
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
