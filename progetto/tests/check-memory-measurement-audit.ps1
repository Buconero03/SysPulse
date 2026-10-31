$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$app = Get-Content -LiteralPath (Join-Path $root 'src\ProcessLens.App\MainWindow.xaml.cs') -Raw -Encoding UTF8
$xaml = Get-Content -LiteralPath (Join-Path $root 'src\ProcessLens.App\MainWindow.xaml') -Raw -Encoding UTF8
$source = Get-Content -LiteralPath (Join-Path $root 'src\ProcessLens.Windows\WindowsSelectedMemoryReader.cs') -Raw -Encoding UTF8
$log = Get-Content -LiteralPath (Join-Path $root 'src\ProcessLens.App\MemoryAnalysisAudit.cs') -Raw -Encoding UTF8
$sem = Get-Content -LiteralPath (Join-Path $root 'src\ProcessLens.Semantics\ProcessQueryEngine.cs') -Raw -Encoding UTF8
$checks = @(
    [PSCustomObject]@{ Name='panel audit'; Passed=$xaml.Contains('ProcessQueryAuditPanel') }
    [PSCustomObject]@{ Name='testo audit'; Passed=$xaml.Contains('ProcessQueryAuditText') }
    [PSCustomObject]@{ Name='refresh per PID'; Passed=$app.Contains('WindowsSelectedMemoryReader.Refresh(row.Process)') }
    [PSCustomObject]@{ Name='contatori Windows'; Passed=$source.Contains('WindowsMemorySampler.Capture') }
    [PSCustomObject]@{ Name='identita PID'; Passed=$source.Contains('process.StartTime != original.StartTime.Value') }
    [PSCustomObject]@{ Name='VirtualQueryEx visibile'; Passed=$app.Contains('AuditStep("VirtualQueryEx"') }
    [PSCustomObject]@{ Name='pressione sistema visibile'; Passed=$app.Contains('AuditStep("GlobalMemoryStatusEx"') }
    [PSCustomObject]@{ Name='log locale'; Passed=$log.Contains('memory-analysis.log') }
    [PSCustomObject]@{ Name='ipotesi esplicite'; Passed=$sem.Contains('COMPONENTI TIPICI · NON MISURATI') }
)
$bad = @($checks | Where-Object { -not $_.Passed })
if ($bad.Count -gt 0) { throw ('FAIL: ' + (($bad | ForEach-Object { $_.Name }) -join ', ')) }
Write-Host 'PASS: selected PID memory refresh, source audit and evidence boundaries.' -ForegroundColor Green
