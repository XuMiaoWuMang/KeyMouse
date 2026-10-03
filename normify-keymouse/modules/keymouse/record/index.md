---
uid: 7a1f0c50
id: keymouse.record
parent: keymouse
tags: [recording, flow, cli]
name: {zh: "录制", en: "Recording"}
description:
  zh: >
      把一次真实操作变成可回放、可编辑的数据，而不是再造一门自动化语言：全局钩子只观察不拦截，每个动作记下它发生在哪个窗口（进程 + 类名，标题只给人看）与客户区相对坐标，轨迹按距离抽稀，关键步骤各存一张小图。流程回放时编译成一条条命令，与手打命令走同一条派发路径。
      
  en: >
      Turns one real session into replayable, editable data instead of inventing another automation language: global hooks observe without swallowing, each action records which window it happened in (process + class, the title only for humans) and client-relative coordinates, the trajectory is thinned by distance, and key steps keep a small screenshot. Replay compiles the flow into the same command lines a human would type.
      
revision: 1b316650150f1540368ee548f7d9992ccaa3239e
updated_at: "2026-10-03T11:27:16.004Z"
fingerprint: 1af9a7d03b6f579038d28ade985b8b59b5f013b9ba04ce988ac66b294129d101
source:
  - path: "src/KeyMouse.Core/Recorder.cs"
  - path: "src/KeyMouse.Core/NativeHooks.cs"
  - path: "src/KeyMouse.Core/FlowModel.cs"
  - path: "src/KeyMouse.Core/FlowRunner.cs"
---
