$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$xamlFile = Join-Path $root 'src\ProcessLens.App\MainWindow.xaml'
$codeFile = Join-Path $root 'src\ProcessLens.App\MainWindow.xaml.cs'
$coreFile = Join-Path $root 'src\ProcessLens.Core\ProcessDetailPresentation.cs'
[xml]$xml = Get-Content -LiteralPath $xamlFile -Raw -Encoding UTF8
$xaml = Get-Content -LiteralPath $xamlFile -Raw -Encoding UTF8
$ui = Get-Content -LiteralPath $codeFile -Raw -Encoding UTF8
$core = Get-Content -LiteralPath $coreFile -Raw -Encoding UTF8
foreach ($name in @('DetailClassificationAuditExpander', 'DetailClassificationAuditPanel', 'DetailEvidenceTechnicalText')) {
    if (-not $xaml.Contains($name)) { throw ('FAIL classification hierarchy: missing XAML control ' + $name) }
}
if (-not $xaml.Contains('Rapporto diagnostico originale') -or -not $xaml.Contains('MaxHeight="230"')) {
    throw 'FAIL classification hierarchy: raw report must remain available with bounded height.'
}
if (-not $ui.Contains('DetailClassificationAudit_Expanded') -or
    -not $ui.Contains('RefreshClassificationAuditGroups') -or
    -not $ui.Contains('ProcessDetailPresentation.ClassificationAuditFacts')) {
    throw 'FAIL classification hierarchy: UI event and formatter are not connected.'
}
if (-not $core.Contains('ClassificationAuditFacts') -or -not $core.Contains('EvidenceDisplayLabel')) {
    throw 'FAIL classification hierarchy: structured evidence presentation is missing.'
}
$invalid = @($xml.SelectNodes("//*[local-name()='TextBox' and @LineHeight]"))
if ($invalid.Count -gt 0) { throw 'FAIL classification hierarchy: invalid TextBox LineHeight (MC3072).' }
$allNames = [regex]::Matches($xaml, 'x:Name="([^"]+)"') | ForEach-Object { $_.Groups[1].Value }
$duplicates = $allNames | Group-Object | Where-Object { $_.Count -gt 1 }
if ($duplicates) { throw 'FAIL classification hierarchy: duplicate XAML control names.' }
Write-Host 'PASS Classification hierarchy v0.19.14: readable groups, preserved original, bounded display.' -ForegroundColor Green
