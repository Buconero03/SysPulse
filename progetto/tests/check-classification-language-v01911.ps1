$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$xaml = Get-Content (Join-Path $root 'src\ProcessLens.App\MainWindow.xaml') -Raw -Encoding UTF8
$ui = Get-Content (Join-Path $root 'src\ProcessLens.App\MainWindow.xaml.cs') -Raw -Encoding UTF8
$core = Get-Content (Join-Path $root 'src\ProcessLens.Core\ProcessDetailPresentation.cs') -Raw -Encoding UTF8
foreach ($key in @('DetailClassificationFactsPanel', 'Rapporto diagnostico originale')) {
    if (-not $xaml.Contains($key)) { throw ('FAIL classification UI: missing ' + $key) }
}
if (-not $ui.Contains('ProcessDetailPresentation.ClassificationFacts')) { throw 'FAIL classification UI: formatter not connected' }
if (-not $core.Contains('ClassificationFacts') -or -not $core.Contains('Limite dell')) {
    throw 'FAIL classification UI: explanation formatter absent'
}
Write-Host 'PASS Classification UI: concise facts, diagnostic details and limit statements.' -ForegroundColor Green

if (-not $core.Contains('Funzione sconosciuta') -or -not $core.Contains('Non indica il lavoro in corso')) {
    throw 'FAIL UX R11: concise unknown/observed boundary missing.'
}
