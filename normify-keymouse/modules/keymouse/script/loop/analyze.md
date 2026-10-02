---
uid: 3d9e0f1a
id: keymouse.script.loop.analyze
parent: keymouse.script.loop
tags: [script, loop]
name: {zh: "循环结构分析与展开计数", en: "Loop analysis & expansion count"}
description:
  zh: >
      一次过扫全脚本：算出每行被哪些循环包着（供变量作用域与执行用）、每个 repeat 对应的 end、以及展开后要执行多少条命令。不配对、次数非法、影子变量、展开超 10 万条——全部在这一步报错，一条命令都还没跑。
      
  en: >
      One pass over the script: which loops enclose each line, each repeat's matching end, and how many commands the loops will dispatch. Unpaired blocks, bad counts, shadowed variables and expansions beyond 100k all fail here, before anything runs.
      
revision: 8d69164cfe5fa0896f595c0bca51f97977ff51b9
updated_at: "2026-10-02T17:37:21.450Z"
fingerprint: 0ef5fdcb1c18d3aaa81d059f6927d97a271aa0ff344d4e213fa1e7d677bd3959
source:
  - path: "ScriptRunner.cs"
    line: 478
    end_line: 561
apis:
  - protocol: rpc
    path: "ScriptRunner.AnalyzeLoops"
    description:
      zh: >
          产出循环计划（含展开总数）。
          
      en: >
          Produces the loop plan, expansion count included.
          
  - protocol: rpc
    path: "LoopPlan"
    description:
      zh: >
          每行的包围循环与配对表。
          
      en: >
          Enclosing loops and matching ends per line.
          
deps:
  - kind: call
    to: keymouse.script.loop.repeat
    from_api: "rpc:ScriptRunner.AnalyzeLoops"
    to_api: "rpc:ScriptRunner.ParseRepeat"
    label: {zh: "解析每个 repeat", en: "Parse each repeat"}
---
