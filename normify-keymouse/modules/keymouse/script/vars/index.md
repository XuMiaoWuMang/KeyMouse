---
uid: 5f1a2b3c
id: keymouse.script.vars
parent: keymouse.script
tags: [script, variables]
name: {zh: "变量", en: "Variables"}
description:
  zh: >
      ${name} 的替换与预校验：变量来自 --set 或所在循环的循环变量；替换发生在分词之后，所以带空格的值仍是一个参数。
      
  en: >
      ${name} substitution and its upfront check: a name comes from --set or from a loop that encloses the line. Substitution happens after tokenising, so a value with spaces stays one argument.
      
revision: 6347fbfbc3d7af8839f5dcc9c35501ce2484bdef
updated_at: "2026-10-03T11:11:56.212Z"
fingerprint: ca45bdaf4b9f154a70f5dbccdacf28399554f02a52725ac899164738ebdf95af
source:
  - path: "src/KeyMouse.Core/ScriptRunner.cs"
    line: 635
    end_line: 716
---
