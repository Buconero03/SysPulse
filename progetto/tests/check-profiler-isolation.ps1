$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$ui = Get-Content -LiteralPath (Join-Path $root 'src\ProcessLens.App\MainWindow.xaml.cs') -Raw -Encoding UTF8
$xaml = Get-Content -LiteralPath (Join-Path $root 'src\ProcessLens.App\MainWindow.xaml') -Raw -Encoding UTF8
$client = Get-Content -LiteralPath (Join-Path $root 'src\ProcessLens.App\ProfilerClient.cs') -Raw -Encoding UTF8
$worker = Get-Content -LiteralPath (Join-Path $root 'src\ProcessLens.Profiler\Program.cs') -Raw -Encoding UTF8
$build = Get-Content -LiteralPath (Join-Path $root 'publish-release.ps1') -Raw -Encoding UTF8
$checks = @{
    'GUI esplicita e consenso' = $xaml.Contains('RunManagedProfiler_Click') -and $ui.Contains('MessageBoxResult.Yes');
    'GUI avvia solo il profiler esterno' = $client.Contains('ProcessLensProfiler.exe') -and $client.Contains('CreateNoWindow = true');
    'Profilazione annullabile' = $ui.Contains('CancelProfiler_Click') -and $client.Contains('Kill(entireProcessTree: true)');
    'RAM gate all 85 percent' = $worker.Contains('ProfilerPolicy.SafeToCollect(used)');
    'Nessun tool auto-installato' = $worker.Contains('dotnet tool install --global dotnet-gcdump') -and (-not $worker.Contains('Process.Start("dotnet tool install'));
    'Dump temporaneo' = $worker.Contains('Directory.Delete(scratch, recursive: true)');
    'Binario profiler separato' = $build.Contains("'Profiler'") -and $build.Contains('ProcessLens.Profiler.csproj')
}
foreach ($entry in $checks.GetEnumerator()) {
    if (-not $entry.Value) { throw ('FAIL profiler isolation: ' + $entry.Key) }
    Write-Host ('PASS profiler isolation: ' + $entry.Key)
}
