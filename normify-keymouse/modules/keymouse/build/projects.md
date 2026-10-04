---
uid: 7a1f0e08
id: keymouse.build.projects
parent: keymouse.build
tags: [build, architecture]
name: {zh: "项目布局", en: "Project layout"}
description:
  zh: >
      三个项目 = 三层：Core（能力层，库）、Runner（常驻引擎，库）、Cli（入口，产物仍是 dist\KeyMouse.exe），编辑器单独一个 WinUI 项目引用前两个。Core 用 InternalsVisibleTo 把内部类型开放给同产品的另两个程序集与测试；`dotnet sln add` 的校验器不认识这个 item，加项目时要临时去掉那一段。
      
  en: >
      Three projects = three layers: Core (capability layer, library), Runner (resident engine, library) and Cli (entry point, still producing dist\KeyMouse.exe), with the editor as a separate WinUI project referencing the first two. Core opens its internals to the product other assemblies and the tests via InternalsVisibleTo; that item makes `dotnet sln add` refuse the project, so it is stripped for the moment the project is added.
      
revision: ef3cf740e80bd62dd7540df6ee4c526380163233
updated_at: "2026-10-04T14:24:35.937Z"
fingerprint: 85470066ff626b95a1cd0034d465ad392c8d675fc608e59c7f00cfc3144daa6b
source:
  - path: "src/KeyMouse.Core/KeyMouse.Core.csproj"
  - path: "src/KeyMouse.Runner/KeyMouse.Runner.csproj"
  - path: "src/KeyMouse.Cli/KeyMouse.Cli.csproj"
  - path: "KeyMouse.sln"
apis:
  - protocol: file
    path: "src/KeyMouse.Core/KeyMouse.Core.csproj"
    description:
      zh: >
          能力层：库，开放内部类型给同产品的另两个程序集与测试。
          
      en: >
          The capability layer: a library that opens its internals to the product other assemblies and the tests.
          
  - protocol: file
    path: "src/KeyMouse.Runner/KeyMouse.Runner.csproj"
    description:
      zh: >
          常驻引擎：库，引用能力层。
          
      en: >
          The resident engine: a library referencing the capability layer.
          
  - protocol: file
    path: "src/KeyMouse.Cli/KeyMouse.Cli.csproj"
    description:
      zh: >
          命令行入口：控制台子系统，产物 dist\KeyMouse.exe。
          
      en: >
          The command-line entry: a console subsystem producing dist\KeyMouse.exe.
          
---
