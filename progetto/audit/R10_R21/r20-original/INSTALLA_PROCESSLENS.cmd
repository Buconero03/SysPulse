@echo off
setlocal
"%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe" -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0setup\install-offline.ps1"
if errorlevel 1 echo Installazione interrotta. Conserva il log indicato sopra.
pause
