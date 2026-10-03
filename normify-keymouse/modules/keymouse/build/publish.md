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
      
revision: e03d53e4c41a8a7220e83123d7f8a44a54ff8dab
updated_at: "2026-10-03T08:15:19.843Z"
fingerprint: 3db0082371e9299af0cd056cd8c9c0753cc6e9ddd75d8f916f54000c32169f40
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
