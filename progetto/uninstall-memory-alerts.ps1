$ErrorActionPreference = 'Stop'
$dest = Join-Path $env:LOCALAPPDATA 'ProcessLens\MemoryAgent'
$target = Join-Path $dest 'ProcessLens.MemoryAgent.exe'
Write-Host 'Disattivazione avvisi RAM per questo utente. Guardian e dati SQLite restano intatti.'
if ((Read-Host 'Digita DISATTIVA per rimuovere l''avvio automatico') -cne 'DISATTIVA') { Write-Host 'Nessuna modifica.'; exit 0 }
$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$v = (Get-ItemProperty -Path $runKey -Name 'ProcessLensMemoryAlerts' -ErrorAction SilentlyContinue).ProcessLensMemoryAlerts
if ($v -eq ('"' + $target + '"')) {
    Remove-ItemProperty -Path $runKey -Name 'ProcessLensMemoryAlerts' -ErrorAction Stop
    Write-Host 'Avvio automatico disabilitato.'
} elseif ($v) { Write-Warning 'Voce di avvio differente: non la modifico.' }
Get-Process -Name 'ProcessLens.MemoryAgent' -ErrorAction SilentlyContinue | ForEach-Object {
    try { if ($_.Path -eq $target) { Stop-Process -Id $_.Id -ErrorAction Stop } }
    catch { Write-Warning $_.Exception.Message }
}
Write-Host 'Agente disattivato. File e log conservati; nessuna modifica alla cronologia Guardian.'
