$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$xamlPath = Join-Path $root 'src\ProcessLens.App\MemoryDashboardView.xaml'
$codePath = Join-Path $root 'src\ProcessLens.App\MemoryDashboardView.xaml.cs'
$statePath = Join-Path $root 'src\ProcessLens.Core\MemoryDashboardPresentation.cs'
[xml]$doc = Get-Content -LiteralPath $xamlPath -Raw -Encoding UTF8
$xaml = Get-Content -LiteralPath $xamlPath -Raw -Encoding UTF8
$code = Get-Content -LiteralPath $codePath -Raw -Encoding UTF8
$state = Get-Content -LiteralPath $statePath -Raw -Encoding UTF8
foreach ($name in @('UsageText', 'AvailableText', 'TotalText', 'MemoryBar', 'PressureBadge', 'PressureText', 'PressureExplanation', 'RefreshStatus', 'AgentStatus', 'AgentExplanation', 'TopMemoryProcesses', 'TopProcessesCaption')) {
    if (-not $xaml.Contains(('x:Name="' + $name + '"'))) { throw ('FAIL RAM UI: manca ' + $name) }
    if (-not $code.Contains($name)) { throw ('FAIL RAM UI: code-behind non usa ' + $name) }
}
if (-not $xaml.Contains('PART_Indicator') -or -not $xaml.Contains('TemplateBinding Foreground') -or -not $code.Contains('MemoryBar.Foreground = accent')) {
    throw 'FAIL RAM UI: barra non collegata alla gravita'
}
if (-not $code.Contains('Registry.GetValue') -or -not $code.Contains('configurazione non conferma') -or -not $state.Contains('non dimostra')) {
    throw 'FAIL RAM UI: confini prova/interpretazione mancanti'
}
$names = [regex]::Matches($xaml, 'x:Name="([^"]+)"') | ForEach-Object { $_.Groups[1].Value }
$duplicates = $names | Group-Object | Where-Object { $_.Count -gt 1 }
if ($duplicates) { throw ('FAIL RAM UI: nomi duplicati ' + ($duplicates.Name -join ', ')) }
Write-Host 'PASS RAM UI v0.19.5: XAML, indicatori, prove, stati agente e nomi unici' -ForegroundColor Green
