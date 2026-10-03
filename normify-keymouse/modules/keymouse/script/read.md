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
      
revision: 6347fbfbc3d7af8839f5dcc9c35501ce2484bdef
updated_at: "2026-10-03T11:11:56.209Z"
fingerprint: ca45bdaf4b9f154a70f5dbccdacf28399554f02a52725ac899164738ebdf95af
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
