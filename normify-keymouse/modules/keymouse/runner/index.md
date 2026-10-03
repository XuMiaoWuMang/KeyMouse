---
uid: 7a1f0e01
id: keymouse.runner
parent: keymouse
tags: [runner, service]
name: {zh: "常驻引擎", en: "Resident engine"}
description:
  zh: >
      一个产品、两个入口里的服务端：接受请求、用与 CLI 完全相同的派发去执行、把过程流回客户端。它带来的是进程内状态——作业排队、暂停/继续/取消、逐步事件、日志流，这些一次性子进程给不了。一次只跑一个作业：输入是全局资源，而把能力层输出变成日志流用的是进程级 Console.SetOut。
      
  en: >
      The service half of "one product, two entry points": it takes requests, executes them through exactly the same dispatch the CLI uses, and streams what happens back. What it buys is in-process state - job queueing, pause/resume/cancel, per-step events, a log stream - which a one-shot child process cannot offer. One job at a time: input is a global resource and the log stream is the process-wide console.
      
revision: 6347fbfbc3d7af8839f5dcc9c35501ce2484bdef
updated_at: "2026-10-03T11:13:17.044Z"
fingerprint: a4f17d036f39db3e044b08b9f430c9ae0b92bafbc2d2c8e36aa331ab935ded81
source:
  - path: "src/KeyMouse.Runner/RunnerHost.cs"
  - path: "src/KeyMouse.Runner/RunnerJob.cs"
  - path: "src/KeyMouse.Runner/PipeServer.cs"
  - path: "src/KeyMouse.Runner/RunnerClient.cs"
  - path: "src/KeyMouse.Runner/RunnerProtocol.cs"
---
