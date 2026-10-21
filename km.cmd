@echo off
rem km —— 让 cmd 里也能直接敲 km；PowerShell 里用 .\km
pwsh -NoProfile -File "%~dp0km.ps1" %*
