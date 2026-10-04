---
uid: 7a1f1008
id: keymouse.editor.splitter
parent: keymouse.editor
tags: [editor, ui]
name: {zh: "可拖分隔条", en: "Draggable splitter"}
description:
  zh: >
      可拖的分隔条：8px 的命中区里画一条 2px 的线（1px 落在小数像素上会被摊薄成看不见）。悬停时显示调整光标并把线变成强调色，拖动时整条变成半透明强调色。三栏的边界与输出面板的上下高度都靠它，MinTarget/MaxTarget 防止把面板拖没。它派生自 Button——没有模板的 ContentControl 不参与命中测试（实测：拖拽永远落到别的控件上）。
      
  en: >
      The draggable divider: an 8px hit area drawing a 2px line (a 1px line lands on a fractional pixel and washes out). Hovering shows the resize cursor and turns the line to the accent colour; dragging turns the whole strip into translucent accent. It resizes the three panes and the output panel's height, with MinTarget/MaxTarget so a pane cannot be dragged away. It derives from Button because a ContentControl with no template does not take part in hit testing.
      
revision: ef3cf740e80bd62dd7540df6ee4c526380163233
updated_at: "2026-10-04T14:24:35.927Z"
fingerprint: f9ec40843419f068495c09203d190a3ca0cc492cfa9094b35a9eca2ccb0ce521
source:
  - path: "editor/KeyMouse.FlowEditor/Splitter.cs"
apis:
  - protocol: rpc
    path: "Splitter.Target"
    description:
      zh: >
          8px 命中区里那条 2px 线，拖动它改分栏比例。
          
      en: >
          The 8px hit area holding the 2px line, dragged to resize a pane.
          
deps:
  - kind: reference
    to: keymouse.editor.shell
    label: {zh: "切开三栏", en: "Splits the three panes"}
---
