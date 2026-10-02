---
uid: f6a7b8c9
id: keymouse.window.select.matcher
parent: keymouse.window.select
tags: [window]
name: {zh: "选择器匹配规则", en: "Selector matching rules"}
description:
  zh: >
      各条件的 AND 组合：标题子串/完全匹配大小写不敏感，类名与进程名精确匹配，进程号与句柄直接比数值；空选择器不匹配任何窗口（宁可报错也不默认“当前窗口”）。
  en: >
      AND of every condition: title substring or exact match, case-insensitive; class and process compared exactly; pid and handle numerically. An empty selector matches nothing - better an error than a silent guess.
revision: 8d69164cfe5fa0896f595c0bca51f97977ff51b9
updated_at: "2026-10-02T18:00:00Z"
fingerprint: e52dac08492f5ae3f8efd6a9e9cbe6e5af9a49c1c1402c8c9bedaf366fa57b9c
source:
  - path: "WindowLocator.cs"
    line: 15
    end_line: 38
apis:
  - protocol: rpc
    path: "WindowSelector.Matches"
    description:
      zh: >
          判断一个窗口是否满足全部条件。
      en: >
          Whether a window satisfies every condition.
  - protocol: rpc
    path: "WindowSelector.Describe"
    description:
      zh: >
          把选择器渲染成可读文本。
      en: >
          Renders the selector as readable text.
---
