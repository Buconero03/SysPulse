@echo off
setlocal EnableExtensions
cd /d "%~dp0"
set "PSEXE=%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe"
if not exist "%PSEXE%" (
  echo ERRORE: Windows PowerShell non trovato.
  pause
  exit /b 1
)
if not exist "%~dp0start-processlens-all.ps1" (
  echo ERRORE: start-processlens-all.ps1 non trovato accanto a questo CMD.
  echo Estrarre TUTTO lo ZIP, non soltanto il file CMD.
  pause
  exit /b 1
)
echo Apro una nuova finestra PowerShell per ProcessLens...
start "ProcessLens - Quick" "%PSEXE%" -NoLogo -NoProfile -NoExit -ExecutionPolicy Bypass -File "%~dp0start-processlens-all.ps1" -Mode Quick
if errorlevel 1 (
  echo Impossibile aprire PowerShell.
  pause
  exit /b 1
)
exit /b 0
