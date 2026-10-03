---
uid: 7a1f0e04
id: keymouse.runner.pipe
parent: keymouse.runner
tags: [runner, ipc]
name: {zh: "管道服务与客户端", en: "Pipe server and client"}
description:
  zh: >
      服务端把每个请求丢进自己的任务（否则长流程期间收不到 cancel），写入加锁串行化；客户端一条连接、一个后台泵，按请求 id 路由事件，`ConnectOrStartAsync` 在没有服务时把 `serve` 拉起来。编辑器用的就是它。
      
  en: >
      The server hands every request to its own task (or a long flow would block its own cancel) and serialises writes behind a lock; the client keeps one connection with a background pump that routes events by request id, and ConnectOrStartAsync starts `serve` when nothing is listening. The editor uses exactly this.
      
revision: 6347fbfbc3d7af8839f5dcc9c35501ce2484bdef
updated_at: "2026-10-03T11:13:17.045Z"
fingerprint: 38308e3ab07cd66aa8bdf0f2244133cceaef77b7dee0967f76ce73eb21011696
source:
  - path: "src/KeyMouse.Runner/PipeServer.cs"
  - path: "src/KeyMouse.Runner/RunnerClient.cs"
apis:
  - protocol: rpc
    path: "PipeServer.RunAsync"
    description:
      zh: >
          接受连接并为每条连接读请求、写事件。
          
      en: >
          Accepts connections and reads requests / writes events per connection.
          
  - protocol: rpc
    path: "RunnerClient.SendAsync"
    description:
      zh: >
          发一个请求并等它的终态事件，过程中回调每个事件。
          
      en: >
          Sends one request, awaits its terminal event, and sees every event on the way.
          
  - protocol: rpc
    path: "RunnerClient.ConnectOrStartAsync"
    description:
      zh: >
          没人在听就启动一个 Runner。
          
      en: >
          Starts a Runner when nobody is listening.
          
---
