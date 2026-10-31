#Requires -Version 5.1
# Read-only Process Activity Intelligence report. Does not stop or modify the installed Guardian.
param(
    [ValidateRange(5,1440)] [int] $Minutes = 30,
    [string] $OutputFile = ''
)
$ErrorActionPreference = 'Stop'
$exe = Join-Path $PSScriptRoot 'dist\Guardian-win-x64\ProcessLens.Guardian.Service.exe'
if (-not (Test-Path -LiteralPath $exe)) { throw 'Compila prima con .\build-guardian.ps1. Nessun servizio verra modificato.' }
$db = Join-Path $env:ProgramData 'ProcessLens\Guardian\guardian.sqlite3'
if (-not (Test-Path -LiteralPath $db)) { throw "Database Guardian non trovato: $db" }
Write-Host "Analisi read-only: $Minutes minuti; servizio non modificato." -ForegroundColor Cyan
if ($OutputFile) {
    # Capture only successful reports; avoid replacing a user's file on failure.
    $content = & $exe --activity $Minutes
    if ($LASTEXITCODE -ne 0) { throw 'Analisi non riuscita; file report non scritto.' }
    $destination = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputFile)
    $directory = Split-Path -Parent $destination
    if (-not (Test-Path $directory)) { throw "Cartella di destinazione assente: $directory" }
    [System.IO.File]::WriteAllText($destination, ($content -join [Environment]::NewLine), [System.Text.UTF8Encoding]::new($false))
    Write-Host "Report scritto in $destination" -ForegroundColor Green
} else {
    & $exe --activity $Minutes
    if ($LASTEXITCODE -ne 0) { throw 'Analisi non riuscita.' }
}
