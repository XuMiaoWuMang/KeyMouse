---
uid: 7a1f0c51
id: keymouse.record.session
parent: keymouse.record
tags: [recording]
name: {zh: "录制会话与步骤合成", en: "Session and step synthesis"}
description:
  zh: >
      把钩子事件流变成步骤：按键按当前布局翻成字符（Unicode 注入也认）、组合键记键名、按下与抬起配对成点击或拖拽、轨迹按阈值抽稀、停顿超过阈值才记 sleep、每个动作解析出所属窗口与相对坐标。步骤截图是**整个客户区缩小 + 动作点红十字**（原地 320×200 裁剪实测在空白窗体上就是一块白）。
      
  en: >
      Turns the hook stream into steps: keys become the characters the active layout produces (Unicode injection included), combinations keep their key names, down/up pairs become a click or a drag, the trajectory is thinned, only pauses past the threshold become sleeps, and every action resolves its window and coordinates. A screenshot is the whole client area scaled down with a mark on the action point (a 320x200 crop measured as blank white on an empty form).
      
revision: 01b8c3c91b12f7561f62aa9f6af36e7eb9e3bb94
updated_at: "2026-10-03T10:33:31.241Z"
fingerprint: 856732460646dab4b6162596fe0da6235d920b921f5cca8e3c854b153156fabe
source:
  - path: "Recorder.cs"
apis:
  - protocol: rpc
    path: "Recorder.Run"
    description:
      zh: >
          录制命令入口：装钩子、跑到停止、写 JSON 与截图。
          
      en: >
          Entry point: install hooks, run until stopped, write the JSON and the screenshots.
          
  - protocol: rpc
    path: "Recorder.ThinTrajectoryIndices"
    description:
      zh: >
          轨迹抽稀规则（保留哪些点）。
          
      en: >
          Which trajectory points survive thinning.
          
  - protocol: rpc
    path: "Recorder.IsDrag"
    description:
      zh: >
          按下到抬起的位移够不够算拖拽。
          
      en: >
          Whether a press-release travelled far enough to be a drag.
          
deps:
  - kind: call
    to: keymouse.native.hooks
    to_api: "rpc:NativeHooks.Record"
    label: {zh: "装钩子并泵消息", en: "Install hooks and pump"}
  - kind: call
    to: keymouse.native.capture
    to_api: "rpc:NativeCapture.TryCaptureScreen"
    label: {zh: "取步骤截图", en: "Take the step shot"}
  - kind: call
    to: keymouse.window.select.point
    to_api: "rpc:WindowLocator.WindowAt"
    label: {zh: "解析动作所属窗口", en: "Resolve the window"}
  - kind: call
    to: keymouse.flow.model
    to_api: "rpc:FlowDocument.Save"
    label: {zh: "写流程文件", en: "Write the flow"}
---
