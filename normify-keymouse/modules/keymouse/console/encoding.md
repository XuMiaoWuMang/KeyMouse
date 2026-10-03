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
      
revision: e03d53e4c41a8a7220e83123d7f8a44a54ff8dab
updated_at: "2026-10-03T08:15:19.854Z"
fingerprint: 27b97c5497a18a075d4b147d795c51c354f85c1b750f67f2fd59ae6ddc0124f1
source:
  - path: "ConsoleText.cs"
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
