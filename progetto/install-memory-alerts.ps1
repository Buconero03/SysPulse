$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

$source = Join-Path $PSScriptRoot 'publish\MemoryAgent-win-x64'
$exe = Join-Path $source 'ProcessLens.MemoryAgent.exe'
if (-not (Test-Path $exe)) { throw 'MemoryAgent non pubblicato. Esegui prima .\build-dashboard.ps1.' }

$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]::new($identity)
if ($principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Per evitare un agente elevato in background, esegui questo installer da PowerShell NORMALE (senza amministratore).'
}

$dest = Join-Path $env:LOCALAPPDATA 'ProcessLens\MemoryAgent'
$target = Join-Path $dest 'ProcessLens.MemoryAgent.exe'
Write-Host 'ProcessLens RAM: soglia >=85%, riarmo solo dopo <80%.' -ForegroundColor Cyan
Write-Host 'Installa agente nell''account utente, mostra avvisi nell''area di notifica e avvia automaticamente all''accesso.'
Write-Host 'Nessun programma viene chiuso o modificato. Nessun cambiamento al servizio Guardian.'
Write-Host ('Percorso agente: ' + $dest)
$answer = Read-Host 'Digita ATTIVA per autorizzare installazione e avvio automatico'
if ($answer -cne 'ATTIVA') { Write-Host 'Nessuna modifica eseguita.'; return }

# Stop only our agent installed for this user; never kill arbitrary processes by name alone.
Get-Process -Name 'ProcessLens.MemoryAgent' -ErrorAction SilentlyContinue | ForEach-Object {
    try { if ($_.Path -eq $target) { Stop-Process -Id $_.Id -ErrorAction Stop; $_.WaitForExit(4000) | Out-Null } }
    catch { Write-Warning ('Impossibile arrestare agente precedente: ' + $_.Exception.Message) }
}
New-Item -ItemType Directory -Path $dest -Force | Out-Null
Copy-Item -Path (Join-Path $source '*') -Destination $dest -Recurse -Force
if (-not (Test-Path $target)) { throw 'Installazione incompleta, exe mancante: ' + $target }
$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
New-Item -Path $runKey -Force | Out-Null
New-ItemProperty -Path $runKey -Name 'ProcessLensMemoryAlerts' -Value ('"' + $target + '"') -PropertyType String -Force | Out-Null
Start-Process -FilePath $target -WorkingDirectory $dest
Write-Host 'Avvisi RAM ATTIVI. L''agente appare nell''area di notifica vicino all''orologio.' -ForegroundColor Green
Write-Host 'Disattivazione: .\uninstall-memory-alerts.ps1 (in PowerShell normale).'
