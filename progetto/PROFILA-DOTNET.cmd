@echo off
setlocal
set "ROOT=%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -NoExit -Command "& '%ROOT%profile-dotnet.ps1'"
