---
uid: 0dbe2c3d
id: keymouse.script.read
parent: keymouse.script
tags: [script, io]
name: {zh: "脚本读取", en: "Script reading"}
description:
  zh: >
      从文件或 stdin 读脚本，统一按 UTF-8 解码并对非法字节给出可读错误；stdin 也是先读成字节再解码，管道里的中文不会乱。
      
  en: >
      Reads a script from a file or stdin, decoding as UTF-8 and reporting bad bytes readably; stdin is read as bytes first so piped Chinese survives.
      
revision: 1b316650150f1540368ee548f7d9992ccaa3239e
updated_at: "2026-10-03T11:27:16.006Z"
fingerprint: 77e8c215319806cea2fd833e231759721bc3125357c377795fe231100c8ddc3f
source:
  - path: "src/KeyMouse.Core/ScriptRunner.cs"
    line: 836
    end_line: 862
apis:
  - protocol: file
    path: "<脚本>.txt"
    description:
      zh: >
          UTF-8 脚本文件（一行一条命令）。
          
      en: >
          A UTF-8 script file, one command per line.
          
  - protocol: rpc
    path: "ScriptRunner.ReadScript"
    description:
      zh: >
          读脚本文件/标准输入。
          
      en: >
          Reads a script file or stdin.
          
---
