---
uid: e3f4a5b6
id: keymouse.cli.options
parent: keymouse.cli
tags: [cli, parsing]
name: {zh: "全局选项解析", en: "Global option parsing"}
description:
  zh: >
      从任意位置抽出全局选项：目标选择器（--title/--class/--process/--pid/--hwnd/--pick）、策略（--focus-policy/--focus-attempts/--allow-restore/--strict-point）与客户区坐标（-wx/-wy/-wx2/--wy2），支持 --flag=value 与 --flag value 两种写法，并将选择器拼成 WindowSelector。
      
  en: >
      Pulls global options out of any position: selectors, focus policy flags and client-area coordinates, accepting both --flag=value and --flag value, then assembles a WindowSelector.
      
revision: 01b8c3c91b12f7561f62aa9f6af36e7eb9e3bb94
updated_at: "2026-10-03T10:33:15.213Z"
fingerprint: bbcd909665ff033ce01c238b5437796aded55168ead822ef453ff228abd0fee5
source:
  - path: "Program.cs"
    line: 546
    end_line: 604
apis:
  - protocol: rpc
    path: "ExtractGlobalOptions"
    description:
      zh: >
          剥离全局选项并返回剩余 argv。
          
      en: >
          Strips global options and returns the remaining argv.
          
  - protocol: rpc
    path: "GlobalOptions.ToSelector"
    description:
      zh: >
          将选项组裝为窗口选择器。
          
      en: >
          Assembles the options into a window selector.
          
---
