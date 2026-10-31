param([int]$TargetPid = 0)
$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot
$exe = Join-Path $PSScriptRoot 'publish\win-x64\Profiler\ProcessLensProfiler.exe'
if (-not (Test-Path -LiteralPath $exe)) { throw 'Compila prima con AVVIA-TUTTO.cmd oppure build-dashboard.ps1.' }
if ($TargetPid -le 4) {
    $rawPid = Read-Host 'PID .NET da profilare (intero positivo)'
    if (-not [int]::TryParse($rawPid, [ref]$TargetPid) -or $TargetPid -le 4) { throw 'PID non valido.' }
}
$target = Get-Process -Id $TargetPid -ErrorAction Stop
$startUtcTicks = $target.StartTime.ToUniversalTime().Ticks
Write-Host "Processo: $($target.ProcessName), PID $TargetPid" -ForegroundColor Cyan
Write-Warning 'Attenzione: dotnet-gcdump attiva una GC Gen2 completa nel processo selezionato e puo aumentare memoria e latenza. Non profilare processi critici o sotto forte pressione RAM.'
Write-Host 'Limiti best effort: 45 secondi, 256 MiB helper, 128 MiB dump, RAM totale inferiore a 85%.'
$confirm = Read-Host 'Scrivi PROFILA per confermare questa singola acquisizione'
if ($confirm -cne 'PROFILA') { Write-Host 'Nessuna profilazione eseguita.'; return }
$session = [Guid]::NewGuid().ToString('N')
$sessionDir = Join-Path $env:TEMP ('ProcessLensProfiler-' + $session)
$report = Join-Path $sessionDir 'result.json'
New-Item -ItemType Directory -Path $sessionDir -Force | Out-Null
try {
    & $exe --mode managed-heap --pid $TargetPid --start-utc-ticks $startUtcTicks --output $report --session-guid $session
    if (-not (Test-Path -LiteralPath $report)) { throw 'Il profiler non ha restituito un report.' }
    $data = Get-Content -LiteralPath $report -Raw -Encoding UTF8 | ConvertFrom-Json
    Write-Host ('[RISULTATO] Stato: ' + $data.Status + ' | Analizzatore: ' + $data.Adapter)
    if ($data.Status -ne 'ok') { Write-Warning 'Nessun risultato heap attendibile: non interpretare i dati come zero memoria.' }
    if ($data.ReasonCode) { Write-Host ('[CODICE] ' + $data.ReasonCode) }
    if ($data.OperationId) { Write-Host ('[OPERAZIONE] ' + $data.OperationId) }
    if ($null -ne $data.ToolExitCode) { Write-Host ('[EXIT CODE gcdump] ' + $data.ToolExitCode) }
    if ($null -ne $data.CaptureDurationMs) { Write-Host ('[DURATA RACCOLTA] ' + $data.CaptureDurationMs + ' ms') }
    if ($null -ne $data.ReportDurationMs) { Write-Host ('[DURATA REPORT] ' + $data.ReportDurationMs + ' ms') }
    if ($null -ne $data.TemporaryFilesRemoved) { Write-Host ('[PULIZIA TEMPORANEI] ' + $data.TemporaryFilesRemoved) }
    Write-Host ('[DETTAGLIO] ' + $data.Detail)
    if ($data.Status -eq 'ok') {
        Write-Host ('[MEMORIA GESTITA .NET] Heap GC (MiB): ' + [math]::Round($data.ManagedHeapBytes / 1MB, 2))
        Write-Host ('[OGGETTI GESTITI] Conteggio: ' + $data.ManagedObjects)
        $data.TopTypes | Select-Object -First 20 TypeName, Bytes, Count | Format-Table -AutoSize
        Write-Host 'Nota: oggetti gestiti, NON tutta la RAM residente.'
    }
} finally {
    Remove-Item -LiteralPath $sessionDir -Recurse -Force -ErrorAction SilentlyContinue
}
