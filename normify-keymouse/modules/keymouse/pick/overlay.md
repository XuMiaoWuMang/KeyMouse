---
uid: 7a1f0c3e
id: keymouse.pick.overlay
parent: keymouse.pick
tags: [perception, interactive, winforms, dpi]
name: {zh: "全屏选区浮层", en: "Full-screen selection overlay"}
description:
  zh: >
      冻结快照当背景（瞄准的东西不会动，浮层自己也不在图里），拖动画矩形、单击选整个客户区、ESC 或右键取消。四条都是实测逼出来的：只设 TopMost 时浮层仍在目标窗口下面；ESC 必须是全局热键，否则后台进程抢不到前台就取消不了；UI 线程的 DPI 必须是 Per-Monitor V2 并当场断言，WinForms 默认把它降成 SystemAware 会让坐标整体被缩放；窗口列表只取一次，否则每次鼠标移动都要枚举约 400 个窗口。
      
  en: >
      A frozen screen grab as backdrop (the target cannot move, the overlay is not in the picture); drag a rectangle, click for a whole client area, ESC or right click to cancel. Four details came from measurement: TopMost alone left it under the target window; ESC must be a global hotkey, as a background process is refused the foreground; the UI thread is asserted per-monitor-V2, since the WinForms default scales coordinates silently; the window list is read once per pick.
      
revision: 6347fbfbc3d7af8839f5dcc9c35501ce2484bdef
updated_at: "2026-10-03T11:11:56.204Z"
fingerprint: 0e3232eab2b5748be46a4ff6c1a41ff0ba41b3c7e9f392404b4243317f75bccb
source:
  - path: "src/KeyMouse.Core/RegionPicker.cs"
    line: 1
    end_line: 303
apis:
  - protocol: rpc
    path: "RegionPicker.Pick"
    description:
      zh: >
          打开浮层并返回人选中的屏幕矩形（取消返回 null）。
          
      en: >
          Shows the overlay and returns the picked screen rectangle, or null on cancel.
          
deps:
  - kind: call
    to: keymouse.window.select.enumerate
    from_api: "rpc:RegionPicker.Pick"
    to_api: "rpc:WindowLocator.EnumerateTopLevel"
    label: {zh: "取一次顶层窗口表供悬停", en: "Read the window list once"}
  - kind: call
    to: keymouse.native.capture
    from_api: "rpc:RegionPicker.Pick"
    to_api: "rpc:NativeCapture.TryCaptureScreen"
    label: {zh: "冻结整屏快照", en: "Freeze the screen"}
  - kind: call
    to: keymouse.pick.placement
    from_api: "rpc:RegionPicker.Pick"
    to_api: "rpc:RegionCommand.ClientBounds"
    label: {zh: "取客户区矩形做高亮", en: "Client rect for hover"}
---
