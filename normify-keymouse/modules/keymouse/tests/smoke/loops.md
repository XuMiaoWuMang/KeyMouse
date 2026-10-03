---
uid: 5c6e7082
id: keymouse.tests.smoke.loops
parent: keymouse.tests.smoke
tags: [test, desktop, invariant]
name: {zh: "循环与单进程保证", en: "Loops & the one-process proof"}
description:
  zh: >
      v2 的两条硬保证在这里被真桌面验证：循环真的按次数写对了行（回读剪贴板逐行比对），以及脚本运行期间**采样到的进程数恒为 1**（如果改成每行一个进程，这里立刻变红）。
      
  en: >
      The two v2 guarantees are verified on a real desktop here: the loop really wrote the right number of lines (read back from the clipboard), and the sampled process count stays exactly 1 for the whole run.
      
revision: 6347fbfbc3d7af8839f5dcc9c35501ce2484bdef
updated_at: "2026-10-03T11:11:56.214Z"
fingerprint: 7003376609f1fbb72481039dd0555357b0030ec8a0f55ec788c8f69683d72cdf
source:
  - path: "tests/smoke.ps1"
    line: 290
    end_line: 343
apis:
  - protocol: rpc
    path: "循环与单进程断言组"
    description:
      zh: >
          循环结果与进程数采样。
          
      en: >
          Loop output and process sampling.
          
deps:
  - kind: call
    to: keymouse.cli.main
    from_api: "rpc:循环与单进程断言组"
    to_api: "rpc:keymouse <命令组>"
    label: {zh: "驱动被测命令", en: "Drive the command"}
---
