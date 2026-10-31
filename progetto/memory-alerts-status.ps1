$key = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$autorun = (Get-ItemProperty -Path $key -Name 'ProcessLensMemoryAlerts' -ErrorAction SilentlyContinue).ProcessLensMemoryAlerts
$running = @(Get-Process -Name 'ProcessLens.MemoryAgent' -ErrorAction SilentlyContinue)
[PSCustomObject]@{
    AutorunUtente = if ($autorun) { $autorun } else { 'Non configurato' }
    AgentiVisibili = $running.Count
    Processo = if ($running.Count) { ($running.Id -join ', ') } else { 'Non in esecuzione' }
    Registro = (Join-Path $env:LOCALAPPDATA 'ProcessLens\memory-agent.log')
} | Format-List
