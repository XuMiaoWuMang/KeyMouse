---
uid: 7a1f1002
id: keymouse.tests.smoke.loops
parent: keymouse.tests.smoke
tags: [test, desktop]
name: {zh: "循环与变量冒烟", en: "Loop and variable smoke"}
description:
  zh: >
      真桌面上：一个列表变量驱动 foreach，把每一项真的打进靶子（回读剪贴板确认三项都在）；read-text 把屏幕上的内容读进变量，后面的步骤用 {{seen}} 把它打出来。实测到的坑也留在这里：读之前留着全选会把置信度从 92.0 打到 65.0，所以先点一下区域外。
      
  en: >
      On a real desktop: a list variable drives a foreach that types every item (the clipboard is read back to prove all three landed), and a read-text captures what is on screen into a variable a later step interpolates. The measured trap lives here too: reading with a selection in place drops confidence from 92.0 to 65.0, so the flow clicks outside the region first.
      
revision: 69321dbdf0d2f94c72a77144dd1c35e063d13815
updated_at: "2026-10-03T11:41:09.822Z"
fingerprint: 7454cb1057eedf4bd977a43b6ccc304041162648ad521c8fe86959b909f4e1c5
source:
  - path: "tests/smoke.ps1"
    line: 689
    end_line: 732
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
