# Explicit opt-in installation. Run as Administrator. Do not call from bootstrap.
#Requires -RunAsAdministrator
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
$name = 'ProcessLensGuardian'
if (Get-Service -Name $name -ErrorAction SilentlyContinue) { throw "$name gia' installato. Usa uninstall-guardian.ps1 prima di una reinstallazione." }
$source = Join-Path $PSScriptRoot 'dist\Guardian-win-x64'
$exe = Join-Path $source 'ProcessLens.Guardian.Service.exe'
if (-not (Test-Path $exe)) { throw 'EXE mancante: esegui prima .\build-guardian.ps1.' }
$target = Join-Path $env:ProgramFiles 'ProcessLens\Guardian'
$data = Join-Path $env:ProgramData 'ProcessLens\Guardian'
Write-Host "Installazione volontaria di $name in $target" -ForegroundColor Cyan
Write-Host 'Account di servizio: LocalSystem (necessario per i kernel event ETW).'
Write-Host "Database locale: $data (configurato con ACL solo SYSTEM/Administrators)."
$confirmation = Read-Host 'Scrivi INSTALLA per confermare'
if ($confirmation -cne 'INSTALLA') { Write-Host 'Installazione annullata.'; return }
New-Item -ItemType Directory -Path $target,$data -Force | Out-Null
Copy-Item (Join-Path $source '*') $target -Recurse -Force
# Deny ordinary users reading potentially sensitive process and socket metadata.
& icacls.exe $data /inheritance:r | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Impossibile impostare ACL directory dati.' }
& icacls.exe $data /grant:r '*S-1-5-18:(OI)(CI)F' '*S-1-5-32-544:(OI)(CI)F' | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'ACL dati non impostate: installazione interrotta.' }
$targetExe = Join-Path $target 'ProcessLens.Guardian.Service.exe'
# SCM uses quoted full path and separate arguments. No hidden install on build.
New-Service -Name $name -DisplayName 'ProcessLens Guardian Collector' `
    -BinaryPathName ('"' + $targetExe + '"') -StartupType Automatic `
    -Description 'Monitoraggio locale di eventi processo, risorse e endpoint. Leggi Guardian.md.' | Out-Null
try {
    Start-Service -Name $name -ErrorAction Stop
    Start-Sleep -Seconds 3
    Get-Service -Name $name | Format-Table Name,Status,StartType
    Write-Host 'Installazione completata. Per rimuovere: .\uninstall-guardian.ps1' -ForegroundColor Green
} catch {
    Write-Warning "Servizio installato ma non avviato: $($_.Exception.Message)"
    Write-Warning "Consulta Visualizzatore eventi > Windows Logs > Application / .\guardian-status.ps1."
}
