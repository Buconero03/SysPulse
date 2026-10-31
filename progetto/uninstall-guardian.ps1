#Requires -RunAsAdministrator
param([switch]$DeleteData)
$ErrorActionPreference = 'Stop'
$name = 'ProcessLensGuardian'
$existing = Get-Service -Name $name -ErrorAction SilentlyContinue
if (-not $existing) { Write-Host 'Guardian non installato.'; return }
$confirmation = Read-Host 'Scrivi RIMUOVI per arrestare e rimuovere il servizio'
if ($confirmation -cne 'RIMUOVI') { Write-Host 'Operazione annullata.'; return }
if ($existing.Status -ne 'Stopped') { Stop-Service $name -ErrorAction Stop; (Get-Service $name).WaitForStatus('Stopped','00:00:20') }
& sc.exe delete $name | Out-Host
if ($LASTEXITCODE -ne 0) { throw 'Eliminazione servizio fallita.' }
$target = Join-Path $env:ProgramFiles 'ProcessLens\Guardian'
for ($i=0;$i -lt 10;$i++) {
    try { if (Test-Path $target) { Remove-Item $target -Recurse -Force -ErrorAction Stop }; break }
    catch { Start-Sleep -Milliseconds 500; if ($i -eq 9) { Write-Warning "File in uso: rimuovere manualmente $target dopo il riavvio." } }
}
$data = Join-Path $env:ProgramData 'ProcessLens\Guardian'
if ($DeleteData) {
    $extra = Read-Host 'Per cancellare DEFINITIVAMENTE anche database, log e report, scrivi CANCELLA DATI'
    if ($extra -ceq 'CANCELLA DATI') { if (Test-Path $data) { Remove-Item $data -Recurse -Force }; Write-Host 'Dati eliminati.' }
    else { Write-Host "Dati conservati in $data" }
} else { Write-Host "Dati conservati in $data; -DeleteData per richiederne la cancellazione." }
Write-Host 'Guardian rimosso. ProcessLens grafico non viene modificato.' -ForegroundColor Green
