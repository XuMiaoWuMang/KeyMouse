---
uid: 7a1f2001
id: keymouse.flow.plan
parent: keymouse.flow
tags: [flow, core]
name: {zh: "执行计划与变量帧", en: "Execution plan and variable frames"}
description:
  zh: >
      把"一份文件要怎么跑"算出来：循环展开、子流程内联，每一步带着自己那一层**变量帧**。帧是链式的（不拷贝），所以子流程看得到调用者的变量、能用自己的 `variables` 覆盖、`vars` 优先级最高，而 `read-text` 的捕获落在最内层——同一份子流程被不同调用者复用也不会串味。`export` 由那个没有步骤的账目项把值搬回调用者那一帧；成环、超过 8 层、文件缺失都在这里被拒。
      
  en: >
      Works out how a file will run: loops expanded, subflows inlined, each step carrying its own variable frame. Frames are chained, not copied - so a subflow sees the caller values, may shadow them with its own variables, loses to the call vars, and keeps its read-text captures to itself, which is what lets one subflow serve different callers. An export is a step-less bookkeeping item that carries values back into the caller frame; cycles, depth over 8 and missing files are refused here.
      
revision: 1ebae14ff2430b597cc4a1695a71ddf788879db1
updated_at: "2026-10-03T12:49:24.516Z"
fingerprint: 990f3efe72a475d91fea74648944ca609f9160957e6e7ed5f38404f47683df30
source:
  - path: "src/KeyMouse.Core/FlowPlan.cs"
apis:
  - protocol: rpc
    path: "FlowPlan.Build"
    description:
      zh: >
          把文档变成线性计划（含内联子流程）。
          
      en: >
          Turns a document into a linear plan (subflows inlined).
          
  - protocol: rpc
    path: "FlowPlan.StepCount"
    description:
      zh: >
          实际会跑的步数（循环与内联之后）。
          
      en: >
          Steps that will really run (after loops and inlining).
          
  - protocol: rpc
    path: "VariableFrame.Lookup"
    description:
      zh: >
          沿帧链向上找一个变量。
          
      en: >
          Looks a variable up the frame chain.
          
  - protocol: rpc
    path: "VariableFrame.Set"
    description:
      zh: >
          在最内层帧里赋值（捕获落在子流程里）。
          
      en: >
          Assigns in the innermost frame (captures stay in the subflow).
          
deps:
  - kind: call
    to: keymouse.flow.model
    to_api: "rpc:FlowDocument.Load"
    label: {zh: "读并校验子流程", en: "Load & validate a subflow"}
  - kind: call
    to: keymouse.flow.model
    to_api: "rpc:FlowDocument.Expand"
    label: {zh: "解析 vars 的值", en: "Resolve the vars values"}
---
