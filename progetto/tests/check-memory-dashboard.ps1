$ErrorActionPreference = 'Stop'
$base = Split-Path $PSScriptRoot -Parent
$xml = Join-Path $base 'src\ProcessLens.App\MemoryDashboardView.xaml'
try { [xml]$doc = Get-Content -Path $xml -Raw } catch { throw ('XAML MemoryDashboard non valido: ' + $_.Exception.Message) }
$c = Get-Content -Path (Join-Path $base 'src\ProcessLens.App\MainWindow.xaml.cs') -Raw
$m = Get-Content -Path (Join-Path $base 'src\ProcessLens.App\MainWindow.xaml') -Raw
$agent = Get-Content -Path (Join-Path $base 'src\ProcessLens.MemoryAgent\Program.cs') -Raw
$memory = Get-Content -Path (Join-Path $base 'src\ProcessLens.Core\MemoryAlertPolicy.cs') -Raw
if ($c -notmatch 'NavMemoryButton_Click' -or $c -notmatch 'MemoryDashboard\?\.SetActive' -or
    $m -notmatch 'MemoryDashboardHost' -or $m -notmatch 'NavMemoryButton' -or
    $memory -notmatch 'usedPercent < ResetPercent' -or $memory -notmatch 'usedPercent >= HighPercent' -or
    $agent -notmatch 'ShowBalloonTip' -or $agent -notmatch 'Interval = 10_000') {
    throw 'Preflight memory dashboard/agent fallito.'
}
Write-Host 'PASS Memory Dashboard: navigazione, XAML, soglia e riarmo, timer leggero e notifica WinForms.' -ForegroundColor Green
