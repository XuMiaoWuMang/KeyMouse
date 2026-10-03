---
uid: 7a1f1002
id: keymouse.tests.smoke.flowloops
parent: keymouse.tests.smoke
tags: [test, desktop]
name: {zh: "循环与变量冒烟", en: "Loop and variable smoke"}
description:
  zh: >
      真桌面上：一个列表变量驱动 foreach，把每一项真的打进靶子（回读剪贴板确认三项都在）；read-text 把屏幕上的内容读进变量，后面的步骤用 {{seen}} 把它打出来。实测到的坑也留在这里：读之前留着全选会把置信度从 92.0 打到 65.0，所以先点一下区域外。
      
  en: >
      On a real desktop: a list variable drives a foreach that types every item (the clipboard is read back to prove all three landed), and a read-text captures what is on screen into a variable a later step interpolates. The measured trap lives here too: reading with a selection in place drops confidence from 92.0 to 65.0, so the flow clicks outside the region first.
      
revision: bc06440df10935e9e8479ecad7b52098b48df7fd
updated_at: "2026-10-03T12:19:12.570Z"
fingerprint: 3ad17b444a177fb19b0a6b0b7476b9da75959dc413a16c656c4ca07c01ca990a
source:
  - path: "tests/smoke/flowloops.ps1"
apis:
  - protocol: rpc
    path: "循环与变量冒烟断言组"
    description:
      zh: >
          foreach 真的执行；read-text 捕获并被后续步骤使用。
          
      en: >
          The foreach really runs; read-text captures and a later step uses it.
          
deps:
  - kind: call
    to: keymouse.flow.runner
    to_api: "rpc:FlowRunner.Run"
    label: {zh: "执行流程", en: "Run the flow"}
---
