---
uid: 7a1f0c55
id: keymouse.flow.runner
parent: keymouse.flow
tags: [flow, cli]
name: {zh: "流程执行器", en: "Flow runner"}
description:
  zh: >
      `run flow.json`：按顺序执行步骤，每一步都走与手打命令相同的派发（含闸门与退出码），失败即停或 `--keep-going`，可重试的退出码（3/4/5/6）按 `--retry` 重试，输出报告与文本脚本同一形状。sleep 在 `--dry-run` 下不真等；wait-window 轮询到窗口可用为止，超时退出码 3；wait-text 还没有实现，遇到就明确报错而不是装作跑过。
      
  en: >
      `run flow.json`: executes steps in order, each through the same dispatch as a typed command (gate and exit codes included), stopping at the first failure or honouring --keep-going, retrying the retryable codes (3/4/5/6) per --retry, and writing the same report shape as the text runner. sleep does not really wait under --dry-run; wait-window polls until a usable window appears and exits 3 on timeout; wait-text is not implemented yet and says so instead of pretending.
      
revision: 8138caf9efa304f3487fcf5c6320ee4ac39f036b
updated_at: "2026-10-03T10:12:03.455Z"
fingerprint: 2498471aac9f04d718e85f9575f848fab9cbdb6b895f85fafffbfc3513273ab6
source:
  - path: "FlowRunner.cs"
apis:
  - protocol: rpc
    path: "FlowRunner.LooksLikeJson"
    description:
      zh: >
          按内容判断 run 拿到的是不是流程（只认第一个参数是路径的形式）。
          
      en: >
          Content-based detection of a flow handed to run (only the documented first-argument form).
          
  - protocol: rpc
    path: "FlowRunner.Run"
    description:
      zh: >
          执行整个流程并返回退出码。
          
      en: >
          Runs the whole flow and returns its exit code.
          
deps:
  - kind: call
    to: keymouse.flow.model
    to_api: "rpc:FlowDocument.ToArguments"
    label: {zh: "步骤编译成命令", en: "Compile a step"}
  - kind: call
    to: keymouse.cli.main
    label: {zh: "同一条派发路径", en: "The same dispatch"}
---
