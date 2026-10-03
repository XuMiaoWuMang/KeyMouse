---
uid: daf74bc3
id: keymouse.probe.preprocess
parent: keymouse.probe
tags: [perception, ocr]
name: {zh: "对比度、放大与白边", en: "Contrast, upscale, padding"}
description:
  zh: >
      三个变换，每个都是被实测逼出来的：以背景众数归一化对比度（百分位拉伸因窗口图标把直方图钉死而失效）、放大加白边（引擎舒适区约 300 DPI，屏幕区域是 96）、手写 BMP 写出器因而无需图像库依赖。放大默认用**最近邻**：每个源像素复制成 N×N 方块，不发明像素，所以 --keep-image 里看不到屏幕上没有的光晕；实测 10 个真实样本 3× 最近邻 9/10 逐字正确，双线性只有 6/10（重影就是在丢分），完全不放大 7/10。
      
  en: >
      Three transforms, each forced by measurement: contrast normalisation against the background mode, an upscale with padding (the engine wants ~300 DPI, a screen region is 96), and a hand-written BMP writer so no image library is needed. The upscale samples nearest by default: each source pixel becomes an N x N block, nothing is invented, so --keep-image shows no halo that was not on screen. Over 10 real samples: 9/10 exact at 3x nearest, 6/10 bilinear, 7/10 with no upscale.
      
revision: 7a1a124eb7b20fcbebed310d012cbf3926b69a0b
updated_at: "2026-10-03T08:34:11.271Z"
fingerprint: b104dda83feba9f56879cb7257c7cd4faa7d2db99e1d0d589c89be55c65a61ae
source:
  - path: "Probe.cs"
    line: 361
    end_line: 513
apis:
  - protocol: rpc
    path: "Probe.Normalize"
    description:
      zh: >
          以背景众数为基准归一化对比度，并把暗背景翻成深字浅底。
          
      en: >
          Normalises contrast against the background mode and flips dark backgrounds to dark-on-light.
          
  - protocol: rpc
    path: "Probe.Preprocess"
    description:
      zh: >
          放大（默认最近邻复制）+ 白边，另可切 bilinear。
          
      en: >
          Upscale (nearest replication by default, bilinear optional) plus a white border.
          
  - protocol: rpc
    path: "Probe.WriteBmp"
    description:
      zh: >
          写出 32 位自上而下的 BMP：40 行文件头取代一个图像库依赖。
          
      en: >
          Writes a 32-bit top-down BMP: 40 lines of header instead of an image library dependency.
          
deps:
  - kind: reference
    to: keymouse.native.capture
    label: {zh: "像素进出", en: "pixels in, pixels out"}
---
