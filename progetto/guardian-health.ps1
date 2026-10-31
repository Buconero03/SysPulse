#Requires -Version 5.1
# Read-only health report. Does not stop, upgrade, or configure the installed Windows service.
param(
    [ValidateRange(5,1440)] [int] $Minutes = 30,
    [string] $OutputFile = ''
)
$ErrorActionPreference = 'Stop'
$exe = Join-Path $PSScriptRoot 'dist\Guardian-win-x64\ProcessLens.Guardian.Service.exe'
if (-not (Test-Path -LiteralPath $exe)) { throw 'Compila prima con .\build-guardian.ps1.' }
$db = Join-Path $env:ProgramData 'ProcessLens\Guardian\guardian.sqlite3'
if (-not (Test-Path -LiteralPath $db)) { throw "Database Guardian non trovato: $db" }
Write-Host "Health check in sola lettura; finestra $Minutes minuti; nessuna modifica al servizio." -ForegroundColor Cyan
if ($OutputFile) {
    $content = & $exe --health $Minutes
    if ($LASTEXITCODE -ne 0) { throw 'Health report fallito; file non scritto.' }
    $destination = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputFile)
    $directory = Split-Path -Parent $destination
    if (-not (Test-Path $directory)) { throw "Cartella assente: $directory" }
    [System.IO.File]::WriteAllText($destination, ($content -join [Environment]::NewLine), [System.Text.UTF8Encoding]::new($false))
    Write-Host "Report scritto in $destination" -ForegroundColor Green
} else {
    & $exe --health $Minutes
    if ($LASTEXITCODE -ne 0) { throw 'Health report fallito.' }
}
