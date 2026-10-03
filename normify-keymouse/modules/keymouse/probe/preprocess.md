---
uid: daf74bc3
id: keymouse.probe.preprocess
parent: keymouse.probe
tags: [perception, ocr]
name: {zh: "对比度、放大与白边", en: "Contrast, upscale, padding"}
description:
  zh: >
      三个变换，全部是**选项**：默认把抓到的原始像素直接交给引擎（`--scale 1 --pad 0`、不归一化）。放大（96→引擎舒适的 ~300 DPI）、白边、对比度归一化（含暗底反相）都按需打开。取样可切 `bilinear`/`nearest`，实测两者在真实语料上没量出精度差别；深底浅字不归一化也能读（实测 72~74 vs 归一化 75~79）。
      
  en: >
      Three transforms, all opt-in: by default the captured pixels go straight to the engine (scale 1, no padding, no normalisation). Upscaling (96 -> the ~300 DPI the engine likes), padding and contrast normalisation (including inverting dark backgrounds) are switched on when wanted. The sampler can be bilinear or nearest; measured, the two show no accuracy difference on the real corpus. Light-on-dark reads without normalisation too (72~74 versus 75~79 with it).
      
revision: 1b316650150f1540368ee548f7d9992ccaa3239e
updated_at: "2026-10-03T11:27:16.003Z"
fingerprint: a2eb516cd040a7a60ad8ddefbc45604cb153a269959d6bfb4c10e52481e8655d
source:
  - path: "src/KeyMouse.Core/Probe.cs"
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
