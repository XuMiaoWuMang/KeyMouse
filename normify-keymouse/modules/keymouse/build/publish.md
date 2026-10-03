---
uid: b16cc1d2
id: keymouse.build.publish
parent: keymouse.build
tags: [build]
name: {zh: "单文件发布", en: "Single-file publish"}
description:
  zh: >
      把程序打成 dist\KeyMouse.exe 单文件：默认框架依赖（需 .NET 运行时，约 240 KB），-SelfContained 打成自包含（约 36 MB）。发布失败直接抛异常，不留下半成品。
      
  en: >
      Publishes dist\KeyMouse.exe: framework-dependent by default (needs the runtime, ~240 KB), or self-contained with -SelfContained (~36 MB). A failed publish throws instead of leaving a half-built artefact.
      
revision: 6347fbfbc3d7af8839f5dcc9c35501ce2484bdef
updated_at: "2026-10-03T11:11:56.194Z"
fingerprint: 4a38374a19bd5137fb4ad6265062b76436c28dd0e3dcee6a2732b177c996a9f7
source:
  - path: "build.ps1"
    line: 1
    end_line: 30
apis:
  - protocol: rpc
    path: ".\\build.ps1"
    description:
      zh: >
          发布单文件 exe 到 dist\。
          
      en: >
          Publishes the single-file exe to dist\.
          
  - protocol: rpc
    path: ".\\build.ps1 -SelfContained"
    description:
      zh: >
          发布自包含版本。
          
      en: >
          Publishes a self-contained build.
          
---
