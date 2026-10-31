$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$xamlPath = Join-Path $root 'src\ProcessLens.App\MainWindow.xaml'
$semanticPath = Join-Path $root 'src\ProcessLens.Semantics\ProcessIntelligenceAnalyzer.cs'
$termPath = Join-Path $root 'src\ProcessLens.Core\ProcessTerminology.cs'
[xml]$document = Get-Content -LiteralPath $xamlPath -Raw -Encoding UTF8
$xaml = Get-Content -LiteralPath $xamlPath -Raw -Encoding UTF8
$semantic = Get-Content -LiteralPath $semanticPath -Raw -Encoding UTF8
$terms = Get-Content -LiteralPath $termPath -Raw -Encoding UTF8
$windowSource = Get-Content -LiteralPath (Join-Path $root 'src\ProcessLens.App\MainWindow.xaml.cs') -Raw -Encoding UTF8

$headings = @('RUOLO STIMATO', 'IDENTITÀ E CLASSIFICAZIONE', 'ELEMENTI DI IDENTIFICAZIONE', 'SE TERMINI IL PROCESSO')
foreach ($heading in $headings) {
    if (-not $xaml.Contains($heading)) { throw ('FAIL Terminologia UI: manca ' + $heading) }
}
if (-not $xaml.Contains('DetailEvidenceTechnicalText') -or -not $xaml.Contains('Come è stato riconosciuto')) {
    throw 'FAIL Terminologia UI: informazioni tecniche non espandibili.'
}
if ($xaml.Contains('COME FUNZIONA') -or $xaml.Contains('A COSA SERVE')) {
    throw 'FAIL Terminologia UI: sezioni verbose ancora presenti.'
}
if ($xaml.Contains('RUOLO / CERTEZZA') -or $xaml.Contains('PRECISIONE DELL''ANALISI')) {
    throw 'FAIL Terminologia UI: etichette precedenti ancora visibili.'
}
if ($xaml.Contains('ATTENDIBILITÀ DELL' + [char]0x2019 + 'IDENTIFICAZIONE') -or
    $windowSource.Contains('Indizi {i.') -or
    $xaml.Contains('ESITO IDENTIFICAZIONE')) {
    throw 'FAIL Terminologia UI: graduazioni astratte ancora nella sintesi.'
}
if (-not $terms.Contains('FunctionLabel') -or -not $terms.Contains('EvidenceStrength')) {
    throw 'FAIL Terminologia UI: terminologia condivisa non trovata.'
}
if (-not $semantic.Contains('ProcessTerminology.FunctionLabel')) {
    throw 'FAIL Terminologia UI: l''analizzatore non usa il vocabolario centrale.'
}
Write-Host 'PASS Terminologia UI: etichette, vocabolario centrale e XAML validi.' -ForegroundColor Green

foreach ($term in @('Ruolo stimato','Funzione sconosciuta')) {
    if (-not $terms.Contains($term)) { throw ('FAIL UX R11: missing term ' + $term) }
}
if ($xaml.Contains('Analisi basata su evidenze') -or $xaml.Contains('I processi più recenti sono mostrati per primi.')) {
    throw 'FAIL UX R11: redundant badge or sorting explanation.'
}
Write-Host 'PASS UX R11: short role labels; redundant badge and sorting prose removed.'
