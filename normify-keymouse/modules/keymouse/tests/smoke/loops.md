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
      
revision: e03d53e4c41a8a7220e83123d7f8a44a54ff8dab
updated_at: "2026-10-03T08:15:19.890Z"
fingerprint: 2b0bcc62780e30e68665cfccee66576416b0b8c5c5aca54381a6d00561dc8637
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
