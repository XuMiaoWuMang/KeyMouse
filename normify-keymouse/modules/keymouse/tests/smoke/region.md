---
uid: 7a1f0c41
id: keymouse.tests.smoke.region
parent: keymouse.tests.smoke
tags: [test, desktop, interactive]
name: {zh: "选区浮层冒烟", en: "Region picker smoke"}
description:
  zh: >
      用 KeyMouse 自己的鼠标与键盘驱动浮层：拖动必须回一个客户区矩形、单击必须回整个客户区、ESC 必须退出码 3。另有一条回归测试钉住 --space window 的原点 bug——同一批屏幕像素从 client 与 window 两个坐标系走必须逐字节相同（修之前两者也相同，但原因是都从客户区原点开始）。
      
  en: >
      Drives the overlay with KeyMouse own mouse and keyboard: a drag must return a client-space rectangle, a click the whole client area, ESC exit code 3. One more check pins the --space window origin bug: the same screen pixels reached through client and window space must be byte-identical (before the fix they also matched, for the wrong reason).
      
revision: eee5c2b2dedd9f702beb19d6a8286d336b3db1a3
updated_at: "2026-10-03T14:07:10.616Z"
fingerprint: 8e71b9dcc0014508c6ea0775f3c4fe83c86466d05548f0291f3d3257e2d2946f
source:
  - path: "tests/smoke/region.ps1"
apis:
  - protocol: rpc
    path: "选区浮层与坐标系回归断言组"
    description:
      zh: >
          拖动 / 单击 / 取消三条路径，加上两个坐标系必须同像素。
          
      en: >
          Drag, click and cancel, plus the two spaces reaching the same pixels.
          
deps:
  - kind: call
    to: keymouse.pick.overlay
    to_api: "rpc:RegionPicker.Pick"
    label: {zh: "驱动浮层", en: "Drive the overlay"}
  - kind: call
    to: keymouse.pick.placement
    to_api: "rpc:RegionCommand.Classify"
    label: {zh: "核对换算结果", en: "Check the conversion"}
  - kind: call
    to: keymouse.probe.capture
    to_api: "rpc:Probe.ParseRegion"
    label: {zh: "按两个坐标系读同一块", en: "Read one region both ways"}
---
