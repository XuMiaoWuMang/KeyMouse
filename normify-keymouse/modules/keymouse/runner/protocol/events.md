---
uid: 7a1f1005
id: keymouse.runner.protocol.events
parent: keymouse.runner.protocol
tags: [runner, protocol, events]
name: {zh: "事件表与问候", en: "Event table and greeting"}
description:
  zh: >
      6 种 kind：hello | log | step | finished | result | error。log 是能力层打到 stdout/stderr 的每一行；step 带 index/total/state/type/shot/exitCode/durationMs，其中 shot 是这一步留下的证据截图路径（先执行后截图，所以它是这一步做完之后的屏幕，抓不到就是 null，绝不编一个）；finished 是执行类方法的终结事件；result 是一问一答的终结事件；error 面向用户、必须是中文。还有一条容易被误用的：连上管道后服务端主动发一条 id 固定为 0 的 hello 问候——它是问候，不是任何请求的应答，客户端不得 await 它。
      
  en: >
      Six kinds: hello | log | step | finished | result | error. log is every line the capability layer prints; step carries index/total/state/type/shot/exitCode/durationMs where shot is the evidence screenshot that step left (captured after it ran; a failed capture is null, never fabricated); finished and result terminate executing and question-answer methods; error is user-facing and Chinese. The connect-time hello has a fixed id of 0: a greeting, not a reply - do not await it.
      
revision: ef3cf740e80bd62dd7540df6ee4c526380163233
updated_at: "2026-10-04T14:24:35.926Z"
fingerprint: 57e2a9866514486ce02dab49e76e8c9be7b4760bb588773d44e29f49204d41b8
source:
  - path: "src/KeyMouse.Runner/RunnerProtocol.cs"
apis:
  - protocol: rpc
    path: "keymouse-runner 事件"
    description:
      zh: >
          {kind, id, ...}：hello | log | step | finished | result | error。
          
      en: >
          Event {kind, id, ...}: hello | log | step | finished | result | error.
          
  - protocol: rpc
    path: "RunnerEvent.Step"
    description:
      zh: >
          一步的开始与结束；结束时带上证据截图路径。
          
      en: >
          A step starting and finishing; the finishing event carries the evidence screenshot path.
          
  - protocol: rpc
    path: "RunnerEvent.Hello"
    description:
      zh: >
          连接后的问候：id 固定 0，不是应答。
          
      en: >
          The connect-time greeting: fixed id 0, not a reply.
          
deps:
  - kind: event
    to: keymouse.flow.evidence
    from_api: "rpc:RunnerEvent.Step"
    label: {zh: "这一步的证据截图", en: "Evidence shot of that step"}
---
