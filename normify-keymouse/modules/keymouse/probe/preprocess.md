---
uid: daf74bc3
id: keymouse.probe.preprocess
parent: keymouse.probe
tags: [perception, ocr]
name: {zh: "对比度、放大与白边", en: "Contrast, upscale, padding"}
description:
  zh: >
      三个变换，每个都是被实测逼出来的：以背景众数归一化对比度（百分位拉伸因窗口图标把直方图钉死而失效）、放大加白边（引擎舒适区约 300 DPI，屏幕区域是 96）、手写 BMP 写出器因而无需图像库依赖。放大默认用**最近邻**：每个源像素复制成 N×N 方块，不发明像素——选它的理由是这个性质，不是"更准"：10 个真实样本里双线性 10/10、最近邻 9/10，差别只有 1 个样本（早先那次"最近邻明显更准"把光标噪声算了进去，已作废，见 tests/evidence）。
      
  en: >
      Three transforms, each forced by measurement: contrast normalisation against the background mode, an upscale with padding, and a hand-written BMP writer so no image library is needed. The upscale samples nearest by default: each source pixel becomes an N x N block, nothing is invented - chosen for that property, not for accuracy: over 10 real samples bilinear scored 10/10 and nearest 9/10, a one-sample gap (an earlier claim that nearest reads better was caret noise; see tests/evidence).
      
revision: b3cd2155a6af67fad97967d1a820875c076ceccf
updated_at: "2026-10-03T08:58:37.640Z"
fingerprint: b1bf95901eb70b6ac9e53ddce6322ef098ce934d9177d7bbbfa68d37069170a1
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
