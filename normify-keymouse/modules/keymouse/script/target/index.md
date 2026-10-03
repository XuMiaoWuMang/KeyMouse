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
      
revision: 8138caf9efa304f3487fcf5c6320ee4ac39f036b
updated_at: "2026-10-03T10:11:54.009Z"
fingerprint: f561ff1a777b64b465d886e85545f84beb07f3a3d116e9cb8bb0148ac7caf04c
source:
  - path: "ScriptRunner.cs"
    line: 717
    end_line: 752
---
