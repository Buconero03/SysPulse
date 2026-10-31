$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$xamlPath = Join-Path $root 'src\ProcessLens.App\MainWindow.xaml'
$corePath = Join-Path $root 'src\ProcessLens.Core\ProcessDetailPresentation.cs'
$uiPath = Join-Path $root 'src\ProcessLens.App\MainWindow.xaml.cs'
[xml]$xml = Get-Content -LiteralPath $xamlPath -Raw -Encoding UTF8
$xaml = Get-Content -LiteralPath $xamlPath -Raw -Encoding UTF8
$core = Get-Content -LiteralPath $corePath -Raw -Encoding UTF8
$ui = Get-Content -LiteralPath $uiPath -Raw -Encoding UTF8
$required = @(
    'DetailTechnicalSectionsExpander',
    'DetailClassificationFactsPanel',
    'Rapporto diagnostico originale',
    'DetailIdentityFactsPanel',
    'DetailResourceFactsPanel',
    'DetailNetworkFactsPanel',
    'DetailEvidenceGroupsPanel',
    'DetailFormattedCommandLineText',
    'DetailCommandLineText',
    'Dati originali completi',
    'Diagnostica completa in formato originale'
)
foreach ($token in $required) {
    if (-not $xaml.Contains($token)) { throw ('FAIL Detail UI: elemento mancante ' + $token) }
}
# Regressione MC3072: TextBox non espone LineHeight come proprieta diretta.
# TextBlock puo usare LineHeight; la formattazione dei TextBox usa TextWrapping e Padding.
$invalidTextBoxes = @($xml.SelectNodes("//*[local-name()='TextBox' and @LineHeight]"))
if ($invalidTextBoxes.Count -gt 0) {
    throw 'FAIL Detail UI: attributo LineHeight non supportato da TextBox (WPF MC3072).'
}
if (-not $xaml.Contains('x:Name="DetailFormattedCommandLineText"')) {
    throw 'FAIL Detail UI: campo dei parametri formattati mancante.'
}
if (-not $core.Contains('FormatCommandLine') -or -not $core.Contains('ParseTechnicalFacts') -or -not $core.Contains('EvidenceGroup')) {
    throw 'FAIL Detail UI: formatter e raggruppamento dati non disponibili.'
}
if (-not $ui.Contains('RefreshTechnicalDetailGroups') -or -not $ui.Contains('ProcessDetailPresentation.FormatCommandLine')) {
    throw 'FAIL Detail UI: sezione di dettaglio non collegata al codice WPF.'
}
$allNames = [regex]::Matches($xaml, 'x:Name="([^"]+)"') | ForEach-Object { $_.Groups[1].Value }
$duplicates = $allNames | Group-Object | Where-Object { $_.Count -gt 1 }
if ($duplicates) { throw 'FAIL Detail UI: nomi XAML duplicati.' }
Write-Host 'PASS Detail UI v0.19.10: sezioni, gerarchia, dati originali e collegamenti WPF.' -ForegroundColor Green
