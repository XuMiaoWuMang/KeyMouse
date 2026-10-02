---
uid: 1a2b3c4d
id: keymouse
parent: null
repository: "https://github.com/XuMiaoWuMang/KeyMouse"
tags: [cli, automation, win32]
name: {zh: "KeyMouse", en: "KeyMouse"}
description:
  zh: >
      Windows 命令行输入模拟工具：一条命令 = 一次真实的鼠标/键盘事件，底层 SendInput，事件进系统输入队列；执行完即退出，无常驻进程。核心是「指定目标窗口 + 动手之前先验证」的 fail-closed 设计：窗口不可用或抢不到前台就一个字节都不发。
  en: >
      Windows CLI that simulates one real mouse/keyboard event per invocation via SendInput, then exits - no daemon. Built around naming a target window and verifying it before anything is sent: fail closed, never guess.
revision: 8d69164cfe5fa0896f595c0bca51f97977ff51b9
updated_at: "2026-10-02T17:50:00Z"
fingerprint: e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855
source: []
deps:
  - kind: call
    to: keymouse.cli
    label: {zh: "入口", en: "Entry point"}
  - kind: call
    to: keymouse.tests
    label: {zh: "回归保护", en: "Regression cover"}
---
