@echo off
rem km —— 根目录唯一入口：cmd 里直接敲 km，PowerShell 里 .\km 或 .\km.cmd
rem 真正的实现在 tools\dev\km.ps1（见 docs/tools/km.md）
pwsh -NoProfile -File "%~dp0tools\dev\km.ps1" %*
