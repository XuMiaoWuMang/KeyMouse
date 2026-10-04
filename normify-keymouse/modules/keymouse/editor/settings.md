---
uid: 7a1f100a
id: keymouse.editor.settings
parent: keymouse.editor
tags: [editor, settings]
name: {zh: "编辑器设置", en: "Editor settings"}
description:
  zh: >
      编辑器自己的小状态：最近打开过哪些流程。空状态如果只说"还没有步骤"，对第一次打开的人是没用的——工具的空状态应该回答"从哪儿开始"，而"上次在改哪个文件"通常就是答案。存在应用数据目录里，跟流程文件无关，删掉它只会丢这份列表；读坏了也不拦启动，当作没有设置。
      
  en: >
      The editor's own small state: which flows were opened recently. An empty state saying only 'no steps yet' is useless to a first-time user - a tool's empty state should answer 'where do I start', and 'the file I was editing' usually is the answer. Kept next to the app data, unrelated to the flow files; deleting it only loses that list, and a settings file that will not parse never blocks startup.
      
revision: ef3cf740e80bd62dd7540df6ee4c526380163233
updated_at: "2026-10-04T14:24:35.927Z"
fingerprint: f32a9893ffa7bc5767abb765ac6076c15b19d9fd638ef73b480a3a3dfba1db8b
source:
  - path: "editor/KeyMouse.FlowEditor/EditorSettings.cs"
apis:
  - protocol: rpc
    path: "EditorSettings.Load"
    description:
      zh: >
          读设置；文件坏了就当作没有设置。
          
      en: >
          Loads the settings; a broken file is treated as no settings at all.
          
  - protocol: rpc
    path: "EditorSettings.Remember"
    description:
      zh: >
          把一个路径提到最近列表的最前（最多 5 条）。
          
      en: >
          Moves a path to the front of the recent list (at most 5).
          
  - protocol: file
    path: "%LOCALAPPDATA%\\KeyMouse\\editor.json"
    description:
      zh: >
          设置文件：%LOCALAPPDATA%\KeyMouse\editor.json。
          
      en: >
          The settings file: %LOCALAPPDATA%\KeyMouse\editor.json.
          
---
