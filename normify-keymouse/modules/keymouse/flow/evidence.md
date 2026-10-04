---
uid: 7a1f100e
id: keymouse.flow.evidence
parent: keymouse.flow
tags: [flow, evidence, shots]
name: {zh: "运行证据截图", en: "Run evidence shots"}
description:
  zh: >
      每一步"当时看到的东西"存成 PNG，落在流程文件旁边的 <流程>.shots/ 里。必须由引擎抓：证据的价值全在"它真的是那一步看到的"，界面不合成、不占位，抓不到就返回 null（宁可这一步没有截图，也不给一张假的）。命名 0001-<类型>.png，每次运行覆盖同一组，所以行上看到的一定是最近一次运行的样子，目录也不会无限长大。
      
  en: >
      Per-step PNGs of what the step actually saw, written next to the flow file in <flow>.shots/. The engine must capture them: evidence is only worth something if it is genuinely what that step saw, so the UI neither composites nor substitutes - a failed capture returns null rather than a fake. Names are 0001-<type>.png and each run overwrites the same set, so a row always shows the latest run and the directory cannot grow forever.
      
revision: ef3cf740e80bd62dd7540df6ee4c526380163233
updated_at: "2026-10-04T14:24:35.925Z"
fingerprint: 46b4a00258f2b2640477914a7343d548b56c6cabcf48139beadb3d1bb2cdfdf9
source:
  - path: "src/KeyMouse.Core/RunShots.cs"
apis:
  - protocol: rpc
    path: "RunShots.Capture"
    description:
      zh: >
          给一步留一张证据；拿不到就返回 null。
          
      en: >
          Captures evidence for one step; returns null when it cannot.
          
  - protocol: rpc
    path: "RunShots.DirectoryOf"
    description:
      zh: >
          证据目录：与用户那份流程同名，后缀 .shots。
          
      en: >
          The evidence directory: the flow's name plus .shots.
          
  - protocol: file
    path: "<流程>.shots/0001-<类型>.png"
    description:
      zh: >
          每一步一张 PNG，每次运行覆盖同一组。
          
      en: >
          One PNG per step, overwritten by each run.
          
deps:
  - kind: dataflow
    to: keymouse.native.capture
    from_api: "rpc:RunShots.Capture"
    to_api: "rpc:NativeCapture.TryCapture"
    label: {zh: "取一张屏幕", en: "Capture the screen"}
---
