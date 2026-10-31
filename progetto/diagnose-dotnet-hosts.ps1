$ErrorActionPreference = 'Stop'

$items = Get-CimInstance Win32_Process |
    Where-Object { $_.Name -in @('dotnet.exe', 'ProcessLens.exe') } |
    Select-Object ProcessId, ParentProcessId, Name, ExecutablePath, CommandLine

if (-not $items) {
    Write-Host 'Nessun dotnet.exe o ProcessLens.exe attivo.' -ForegroundColor Green
    exit 0
}

$items | Format-List

Write-Host ''
Write-Host 'Nota:' -ForegroundColor Cyan
Write-Host '- La GUI ProcessLens deve apparire come ProcessLens.exe.'
Write-Host '- VBCSCompiler/MSBuild sono build server .NET, non finestre ProcessLens.'
Write-Host '- Per chiudere i build server dopo una compilazione: dotnet build-server shutdown'
