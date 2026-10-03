---
uid: eb085cd4
id: keymouse.probe.engine
parent: keymouse.probe
tags: [perception, ocr, external-process]
name: {zh: "外部 OCR 引擎调用", en: "External OCR engine"}
description:
  zh: >
      调用显式配置的外部引擎（默认 Tesseract）并读 tsv 拿逐词置信度。不打包任何模型；引擎版本、tessdata 目录与模型体积都进输出，否则事后“为什么昨天读得出”无从回答。模型目录缺少 configs/ 时根本不会产出 tsv——这个坑花掉过一整轮调试。
      
  en: >
      Invokes the explicitly configured external engine (Tesseract by default) and reads tsv for per-word confidence. Nothing is bundled; engine version, tessdata directory and model sizes go into the output, or a later 'why did this work yesterday' has no answer. A model directory without configs/ produces no tsv at all - that trap cost a whole debugging round.
      
revision: 1b316650150f1540368ee548f7d9992ccaa3239e
updated_at: "2026-10-03T11:27:19.954Z"
fingerprint: a2eb516cd040a7a60ad8ddefbc45604cb153a269959d6bfb4c10e52481e8655d
source:
  - path: "src/KeyMouse.Core/Probe.cs"
    line: 491
    end_line: 628
apis:
  - protocol: rpc
    path: "Probe.ReadOnce"
    description:
      zh: >
          读一次区域并返回文字（谓词每轮用它）。
          
      en: >
          Reads a region once and returns the text (one poll of the predicate).
          
  - protocol: rpc
    path: "Probe.CaptureRegion"
    description:
      zh: >
          按坐标系抓一块区域（CLI 与谓词共用同一规则）。
          
      en: >
          Captures one region for the addressed space (shared by the CLI and the predicate).
          
  - protocol: rpc
    path: "Probe.ValidateRegion"
    description:
      zh: >
          区域越界检查（CLI 与谓词共用同一条规则）。
          
      en: >
          Out-of-bounds check shared by the CLI and the predicate.
          
---
