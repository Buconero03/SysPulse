param(
    [ValidateSet('all', 'startup', 'memory-analysis', 'memory-agent', 'profiler', 'build')]
    [string]$Stream = 'all',
    [ValidateRange(1, 100)] [int]$Last = 20
)
$ErrorActionPreference = 'Stop'
$dir = Join-Path $env:LOCALAPPDATA 'ProcessLens\logs'
$streams = if ($Stream -eq 'all') { @('build','startup','memory-analysis','memory-agent','profiler') } else { @($Stream) }
Write-Host ('ProcessLens - Diagnostica strutturata (ultimi ' + $Last + ' eventi per registro)') -ForegroundColor Cyan
Write-Host ('Cartella: ' + $dir)
foreach ($current in $streams) {
    $file = Join-Path $dir ($current + '.jsonl')
    Write-Host ''
    Write-Host ('REGISTRO: ' + $current) -ForegroundColor Cyan
    if (-not (Test-Path -LiteralPath $file)) { Write-Host 'Nessun evento strutturato registrato.'; continue }
    $events = @(Get-Content -LiteralPath $file -Tail $Last -Encoding UTF8)
    foreach ($eventLine in $events) {
        if ([string]::IsNullOrWhiteSpace($eventLine)) { continue }
        try {
            $e = $eventLine | ConvertFrom-Json -ErrorAction Stop
            $stamp = if ($e.timestamp_utc) { $e.timestamp_utc } else { 'data non disponibile' }
            $code = if ($e.event_id) { $e.event_id } else { 'evento non identificato' }
            $stage = if ($e.stage) { $e.stage } else { $e.operation }
            Write-Host ('[' + $stamp + '] ' + $code + ' | ' + $e.severity + ' | ' + $e.status + ' | ' + $stage)
            if ($e.reason_code) { Write-Host ('  Motivo: ' + $e.reason_code) }
            if ($e.compiler_code) { Write-Host ('  Compilatore: ' + $e.compiler_code) }
            if ($e.source_file_name) { Write-Host ('  File: ' + $e.source_file_name + ' | Riga: ' + $e.source_line) }
            if ($null -ne $e.native_exit_code) { Write-Host ('  Exit code: ' + $e.native_exit_code) }
            if ($e.exception_type) { Write-Host ('  Eccezione: ' + $e.exception_type + ' | HRESULT: ' + $e.h_result_hex) }
        } catch { Write-Host '[AVVISO] Riga JSONL non valida. Evento ignorato.' -ForegroundColor Yellow }
    }
}
Write-Host ''
Write-Host 'I registri originali e i dettagli completi di dotnet restano locali. Non condividere gli originali senza verificarne il contenuto.'
