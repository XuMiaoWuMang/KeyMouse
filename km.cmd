@echo off
rem km - the only entry at the repo root. Type "km" in cmd, or ".\km" in PowerShell.
rem The real implementation lives in tools\dev\km.ps1 (see docs/tools/km.md).
rem
rem Keep this file ASCII-only: cmd parses .cmd bytes with the console code page
rem (936 on zh-CN), so UTF-8 comments in here turn into garbage commands. That
rem bug shipped once: ".\km push" printed two "not recognized as an internal or
rem external command" lines before doing its work.
pwsh -NoProfile -File "%~dp0tools\dev\km.ps1" %*
