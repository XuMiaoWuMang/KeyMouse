---
uid: 7a1f0e04
id: keymouse.runner.pipe
parent: keymouse.runner
tags: [runner, ipc]
name: {zh: "管道服务与客户端", en: "Pipe server and client"}
description:
  zh: >
      服务端把每个请求丢进自己的任务（否则长流程期间收不到 cancel），写入加锁串行化，连上就先发一条 id=0 的问候（是问候，不是应答）；客户端一条连接、一个后台泵，按请求 id 路由事件，并在每个请求上自报家门（client / version）。ConnectOrStartAsync 在没有服务时把 serve 拉起来，并且必须把子进程的输出读干——只重定向不读，缓冲区写满会把 serve 卡死，它连命名管道都建不出来（实测）；读干之后还能把最后几行遗言带给用户。
      
  en: >
      The server gives every request its own task (or a long flow blocks its own cancel), serialises writes and greets a fresh connection with an id=0 hello - a greeting, not a reply; the client keeps one connection, routes events by request id and declares client/version on every request. Starting serve must drain the child's output: redirect without reading and the buffer fills, blocking serve before it can create the pipe (measured); draining also brings its last words to the user.
      
revision: ef3cf740e80bd62dd7540df6ee4c526380163233
updated_at: "2026-10-04T14:24:35.931Z"
fingerprint: d4a1087243e5c41b4b506b8abd1c0ed424c2e1aad1de64cda4d7e9e1f86b4879
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
          没人在听就启动一个 Runner，并把它的输出读干。
          
      en: >
          Starts a Runner when nobody is listening, draining its output.
          
  - protocol: rpc
    path: "RunnerClient.LastStartFailure"
    description:
      zh: >
          上一次启动失败的原因，带给用户（成功时为 null）。
          
      en: >
          Why the last start failed, carried to the user (null when it worked).
          
  - protocol: rpc
    path: "RunnerClient.ClientName"
    description:
      zh: >
          谁在问：入口程序集名，盖在每个请求上。
          
      en: >
          Who is asking: the entry assembly's name, stamped on every request.
          
deps:
  - kind: call
    to: keymouse.runner.protocol.version
    from_api: "rpc:RunnerClient.SendAsync"
    to_api: "rpc:RunnerProtocol.AcceptsVersion"
    label: {zh: "自报家门与版本", en: "Declares who and version"}
---
