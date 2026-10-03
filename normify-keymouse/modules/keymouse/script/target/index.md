---
uid: 8c3d4e5f
id: keymouse.script.target
parent: keymouse.script
tags: [script, target]
name: {zh: "目标继承", en: "Target inheritance"}
description:
  zh: >
      脚本内只写一次选择器：任何带目标选项的行成为“当前目标”，之后的 mouse/key 行不带选择器时自动继承。跨进程不继承——那需要磁盘上的隐藏状态，正是这个工具最该躲的失败模式。
      
  en: >
      Write the selector once per script: any line carrying target options becomes the current target and later mouse/key lines inherit it. Cross-process inheritance is refused, since it would need hidden on-disk state - the failure mode this tool exists to avoid.
      
revision: 1b316650150f1540368ee548f7d9992ccaa3239e
updated_at: "2026-10-03T11:27:16.008Z"
fingerprint: 77e8c215319806cea2fd833e231759721bc3125357c377795fe231100c8ddc3f
source:
  - path: "src/KeyMouse.Core/ScriptRunner.cs"
    line: 717
    end_line: 752
---
