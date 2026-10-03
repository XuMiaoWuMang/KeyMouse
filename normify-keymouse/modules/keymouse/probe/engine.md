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
      
revision: df4356ba04850d65df87f2c6f6d4e83f34b0f709
updated_at: "2026-10-03T09:20:55.385Z"
fingerprint: dd58b0d5dc475cdcfaeef44895e79dfaf8b57665d5fe95998f3117b855ff9692
source:
  - path: "Probe.cs"
    line: 491
    end_line: 628
apis:
  - protocol: rpc
    path: "Probe.RunEngine"
    description:
      zh: >
          调用外部引擎并把 tsv 解析成带置信度的词。
          
      en: >
          Runs the external engine and parses tsv into words with confidence.
          
  - protocol: rpc
    path: "Probe.ResolveEngine"
    description:
      zh: >
          把裸引擎名按常见安装位置解析成可执行路径。
          
      en: >
          Turns a bare engine name into a runnable path via the well-known install locations.
          
  - protocol: rpc
    path: "Probe.DescribeEngine"
    description:
      zh: >
          描述引擎：版本、tessdata 目录、模型文件与体积。
          
      en: >
          Describes the engine: version, tessdata directory, model files and sizes.
          
---
