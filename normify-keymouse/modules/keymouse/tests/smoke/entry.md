---
uid: 182a3c4e
id: keymouse.tests.smoke.entry
parent: keymouse.tests.smoke
tags: [test, desktop]
name: {zh: "入口、编码守卫与靶子启动", en: "Entry, encoding guard & target"}
description:
  zh: >
      脚本开头：校验 exe 与靶子存在、做一次中文往返检查（宿主解码不一致就只报一条清晰错误，而不是让十几条中文断言莫名其妙地失败）、记下剪贴板并启动靶子窗口。
      
  en: >
      The opening moves: check that the exe and the target exist, run one Chinese round-trip probe (a mismatched host decoder gets one clear error instead of a dozen puzzling failures), save the clipboard and start the target window.
      
revision: 69321dbdf0d2f94c72a77144dd1c35e063d13815
updated_at: "2026-10-03T11:41:01.206Z"
fingerprint: 7454cb1057eedf4bd977a43b6ccc304041162648ad521c8fe86959b909f4e1c5
source:
  - path: "tests/smoke.ps1"
    line: 1
    end_line: 76
apis:
  - protocol: rpc
    path: "pwsh tests/smoke.ps1"
    description:
      zh: >
          跑完桌面冒烟（需要交互式桌面）。
          
      en: >
          Runs the desktop smoke suite; needs an interactive desktop.
          
deps:
  - kind: call
    to: keymouse.tests.smoke-target
    from_api: "rpc:pwsh tests/smoke.ps1"
    to_api: "rpc:KeyMouse.SmokeTarget.exe"
    label: {zh: "启动靶子", en: "Start the target"}
---
