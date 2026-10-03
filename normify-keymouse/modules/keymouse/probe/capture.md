---
uid: c9e63ab2
id: keymouse.probe.capture
parent: keymouse.probe
tags: [perception]
name: {zh: "区域定位与采集", en: "Region mapping and capture"}
description:
  zh: >
      把客户区相对坐标映射到取像请求上，用真实客户区尺寸校验，越界就拒绝而不是截断。这个边界检查就是工具态度的缩影：不猜、不静默截断。
      
  en: >
      Maps client-relative coordinates onto the capture, checks them against the real client size and refuses out-of-bounds regions instead of truncating them. The bounds check is the tool's stance in miniature: no guessing, no silent clamping.
      
revision: d870b0c5dfd7fe29675863c0288b572a6111a8d4
updated_at: "2026-10-03T07:31:02.526Z"
fingerprint: c8a37194838b7eb26afe170b7729b9947ba9a014cdcb0881b2b4e87d9c1aa6aa
source:
  - path: "Probe.cs"
    line: 253
    end_line: 281
apis:
  - protocol: rpc
    path: "Probe.ParseRegion"
    description:
      zh: >
          解析客户区坐标的 “x,y,w,h”，其余一律当参数错误拒绝。
          
      en: >
          Parses "x,y,w,h" in client coordinates, rejecting anything else as a usage error.
          
deps:
  - kind: call
    to: keymouse.native.capture
    from_api: "rpc:Probe.ParseRegion"
    to_api: "rpc:NativeCapture.TryCaptureScreen"
    label: {zh: "按校验过的矩形取像素", en: "Capture the checked rectangle"}
---
