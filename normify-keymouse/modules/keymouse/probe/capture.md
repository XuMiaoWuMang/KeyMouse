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
      
revision: 7a1a124eb7b20fcbebed310d012cbf3926b69a0b
updated_at: "2026-10-03T08:34:07.493Z"
fingerprint: b104dda83feba9f56879cb7257c7cd4faa7d2db99e1d0d589c89be55c65a61ae
source:
  - path: "Probe.cs"
    line: 294
    end_line: 322
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
