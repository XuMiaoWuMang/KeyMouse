---
uid: daf74bc3
id: keymouse.probe.preprocess
parent: keymouse.probe
tags: [perception, ocr]
name: {zh: "对比度、放大与白边", en: "Contrast, upscale, padding"}
description:
  zh: >
      三个变换，每个都是被实测逼出来的：以背景众数归一化对比度（百分位拉伸因窗口图标把直方图钉死而失效）、3× 双线性放大加白边（引擎舒适区约 300 DPI，屏幕区域是 96）、手写 BMP 写出器因而无需图像库依赖。
      
  en: >
      Three transforms, each forced by a measurement: contrast normalisation against the background mode (a percentile stretch failed because the window icon pinned the histogram), 3x bilinear upscale with padding (the engine wants ~300 DPI, a screen region is 96), and a hand-written BMP writer so no image library is needed.
      
revision: e03d53e4c41a8a7220e83123d7f8a44a54ff8dab
updated_at: "2026-10-03T08:15:51.237Z"
fingerprint: 55ffec1c5fe76cdac87f8fdb9317271575c268abf8197580f32d864301166b94
source:
  - path: "Probe.cs"
    line: 355
    end_line: 488
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
          3× 双线性放大加白边。
          
      en: >
          3x bilinear upscale plus a white border.
          
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
