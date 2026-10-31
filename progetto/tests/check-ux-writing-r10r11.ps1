$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$terms = Get-Content (Join-Path $root 'src\ProcessLens.Core\ProcessTerminology.cs') -Raw -Encoding UTF8
$query = Get-Content (Join-Path $root 'src\ProcessLens.Semantics\ProcessQueryEngine.cs') -Raw -Encoding UTF8
$view = Get-Content (Join-Path $root 'src\ProcessLens.App\MainWindow.xaml') -Raw -Encoding UTF8
$audit = Get-Content (Join-Path $root 'src\ProcessLens.App\MemoryAnalysisAudit.cs') -Raw -Encoding UTF8
$parser = Get-Content (Join-Path $root 'src\ProcessLens.App\AuditLogPresentation.cs') -Raw -Encoding UTF8
foreach ($term in @('Ruolo stimato','Funzione sconosciuta')) {
    if (-not $terms.Contains($term)) { throw "FAIL UX R11: missing $term" }
}
foreach ($old in @('SALDO IN DIMINUZIONE','SALDO QUASI INVARIATO','heap/strutture dati dinamiche RW','La quota principale dello spazio impegnato')) {
    if ($query.Contains($old)) { throw "FAIL UX R11: old memory wording $old" }
}
if ($view.Contains('Analisi basata su evidenze') -or
    -not $view.Contains('Text="Seleziona un processo per analizzarlo."') -or
    -not $audit.Contains('AuditLogPresentation.FormatRamEntry(timestamp, stage, status)') -or
    -not $parser.Contains('RAM|VERIFICA RAM')) {
    throw 'FAIL UX R11: badge, process hint, compact audit format or legacy parser.'
}
Write-Host 'PASS UX R11: concise terminology and memory summary; compact logs with legacy parsing. Source checks only.'
