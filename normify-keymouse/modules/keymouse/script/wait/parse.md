---
uid: da8c9dae
id: keymouse.script.wait.parse
parent: keymouse.script.wait
tags: [script, wait]
name: {zh: "wait 参数解析", en: "Wait argument parsing"}
description:
  zh: >
      复用命令行的全局选项解析拿到选择器，再读 --timeout / --interval；缺选择器时给出明确错误而不是默默等下去。
      
  en: >
      Reuses the command line's global option parsing for the selector, then reads --timeout and --interval; a missing selector is an error rather than an endless wait.
      
revision: 8d69164cfe5fa0896f595c0bca51f97977ff51b9
updated_at: "2026-10-02T17:37:21.450Z"
fingerprint: 0ef5fdcb1c18d3aaa81d059f6927d97a271aa0ff344d4e213fa1e7d677bd3959
source:
  - path: "ScriptRunner.cs"
    line: 778
    end_line: 817
apis:
  - protocol: rpc
    path: "waitfor <选择器>"
    description:
      zh: >
          等到窗口出现。
          
      en: >
          Waits for a window to appear.
          
  - protocol: rpc
    path: "waitgone <选择器>"
    description:
      zh: >
          等到窗口消失。
          
      en: >
          Waits for a window to disappear.
          
  - protocol: rpc
    path: "ScriptRunner.ParseWait"
    description:
      zh: >
          解析 wait 行的参数。
          
      en: >
          Parses a wait line's arguments.
          
deps:
  - kind: call
    to: keymouse.cli.options
    from_api: "rpc:ScriptRunner.ParseWait"
    to_api: "rpc:ExtractGlobalOptions"
    label: {zh: "复用选择器解析", en: "Reuse selector parsing"}
---
