---
uid: 8e9f0a1b
id: keymouse.console.encoding
parent: keymouse.console
tags: [console, i18n]
name: {zh: "输出编码适配", en: "Output encoding"}
description:
  zh: >
      把 stdout 切到控制台自己的码页：PowerShell 用 [Console]::OutputEncoding 解码原生命令输出，而 .NET 在重定向时默认写 UTF-8——中文 Windows 上于是“已移动到”变成“宸茬Щ鍔ㄥ埌”。无控制台时（CI/管道）保持 UTF-8 默认。
      
  en: >
      Switches stdout to the console's own code page: PowerShell decodes native output with [Console]::OutputEncoding while .NET defaults to UTF-8 when redirected, which turned Chinese into mojibake on a cp936 console. With no console attached the UTF-8 default is already right.
      
revision: 6347fbfbc3d7af8839f5dcc9c35501ce2484bdef
updated_at: "2026-10-03T11:11:56.198Z"
fingerprint: 7b34359e8593529cec65ec16fb52cabdc1ce2520355312e65521683c77dfcee0
source:
  - path: "src/KeyMouse.Core/ConsoleText.cs"
    line: 13
    end_line: 44
apis:
  - protocol: rpc
    path: "ConsoleText.ConfigureOutputEncoding"
    description:
      zh: >
          按控制台码页配置输出编码。
          
      en: >
          Configures output to the console code page.
          
  - protocol: rpc
    path: "kernel32!GetConsoleOutputCP"
    description:
      zh: >
          读控制台输出码页。
          
      en: >
          Reads the console output code page.
          
---
