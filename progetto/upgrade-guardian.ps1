# Replaces only the deployed Guardian executable and dependencies; preserves SQLite under ProgramData.
#Requires -RunAsAdministrator
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
$name = 'ProcessLensGuardian'
$service = Get-Service -Name $name -ErrorAction SilentlyContinue
if ($null -eq $service) { throw 'Guardian non installato. Esegui install-guardian.ps1 solo dopo aver compilato.' }
$source = Join-Path $PSScriptRoot 'dist\Guardian-win-x64'
$exe = Join-Path $source 'ProcessLens.Guardian.Service.exe'
if (-not (Test-Path -LiteralPath $exe)) { throw 'Prima esegui .\build-guardian.ps1 con esito positivo.' }
$target = Join-Path $env:ProgramFiles 'ProcessLens\Guardian'
$targetExe = Join-Path $target 'ProcessLens.Guardian.Service.exe'
$scmInfo = Get-CimInstance Win32_Service -Filter "Name='ProcessLensGuardian'"
if ($null -eq $scmInfo) { throw 'Servizio non trovato nel Service Control Manager.' }
# Never overwrite another unrelated executable registered under this service name.
$configured = $scmInfo.PathName.Trim().Trim('"')
if (-not [string]::Equals($configured, $targetExe, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Percorso eseguibile inatteso: $configured. Aggiornamento rifiutato."
}
if (-not (Test-Path -LiteralPath $targetExe)) { throw "EXE installato mancante: $targetExe" }
Write-Host "Aggiornamento di $name. Il monitoraggio si interrompera' brevemente." -ForegroundColor Cyan
Write-Host "Database intatto: $(Join-Path $env:ProgramData 'ProcessLens\Guardian\guardian.sqlite3')"
$confirm = Read-Host 'Scrivi AGGIORNA per proseguire'
if ($confirm -cne 'AGGIORNA') { Write-Host 'Nessuna modifica eseguita.'; return }
$backups = Join-Path $env:ProgramData 'ProcessLens\GuardianDeployBackups'
New-Item -ItemType Directory -Path $backups -Force | Out-Null
$backup = Join-Path $backups ('backup-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
$wasRunning = $service.Status -eq 'Running'
# Stop only our own service; never stop kernel ETW sessions or unrelated services.
if ($service.Status -ne 'Stopped') {
    Stop-Service -Name $name -ErrorAction Stop
    (Get-Service -Name $name).WaitForStatus('Stopped', [TimeSpan]::FromSeconds(30))
}
Copy-Item -LiteralPath $target -Destination $backup -Recurse -Force -ErrorAction Stop
Write-Host "Backup della distribuzione precedente: $backup"
try {
    # Do not touch ProgramData/ProcessLens/Guardian: it holds the existing SQLite database.
    Copy-Item -Path (Join-Path $source '*') -Destination $target -Recurse -Force -ErrorAction Stop
    Start-Service -Name $name -ErrorAction Stop
    (Get-Service -Name $name).WaitForStatus('Running', [TimeSpan]::FromSeconds(30))
    Start-Sleep -Seconds 3
    if ((Get-Service $name).Status -ne 'Running') { throw 'Guardian si e arrestato subito dopo il riavvio.' }
    Get-Service $name | Format-Table Name,Status,StartType
    Write-Host 'Aggiornamento eseguito. Verificare ETW nel registro Applicazione e in diagnostics SQLite.' -ForegroundColor Green
}
catch {
    $problem = $_.Exception.Message
    Write-Warning "Upgrade non riuscito ($problem). Tentativo di ripristino dei binari precedenti."
    try {
        if ((Get-Service $name).Status -ne 'Stopped') {
            Stop-Service $name -ErrorAction SilentlyContinue
            (Get-Service $name).WaitForStatus('Stopped', [TimeSpan]::FromSeconds(30))
        }
        Copy-Item -Path (Join-Path $backup '*') -Destination $target -Recurse -Force -ErrorAction Stop
        if ($wasRunning) { Start-Service -Name $name -ErrorAction Stop }
        Write-Warning 'Binari precedenti ripristinati. Database non modificato.'
    } catch {
        Write-Warning "Ripristino non completato: $($_.Exception.Message). Backup disponibile in: $backup"
    }
    throw "Aggiornamento Guardian fallito: $problem"
}
