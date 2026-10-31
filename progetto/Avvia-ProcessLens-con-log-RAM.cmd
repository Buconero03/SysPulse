@echo off
powershell.exe -NoProfile -STA -File "%~dp0start-processlens-ram-monitor.ps1" -StartGui
if errorlevel 1 pause
