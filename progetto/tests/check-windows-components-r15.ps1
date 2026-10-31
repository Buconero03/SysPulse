$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$core = Get-Content -LiteralPath (Join-Path $root 'src\ProcessLens.Core\WindowsComponentCatalog.cs') -Raw -Encoding UTF8
$code = Get-Content -LiteralPath (Join-Path $root 'src\ProcessLens.App\SystemProcessesView.xaml.cs') -Raw -Encoding UTF8
[xml]$view = Get-Content -LiteralPath (Join-Path $root 'src\ProcessLens.App\SystemProcessesView.xaml') -Raw -Encoding UTF8
foreach ($token in @('WindowsComponentCatalog.Retain(_source.Capture(operation.Token), _windowsDirectory)',
 'WindowsComponentCatalog.Groups(c.Processes', 'SystemInstancesBox.ItemsSource = null;',
 'SystemInstancesBox.SelectedItem is SystemProcessRow { Entry.StartUtc: not null }')) {
 if (-not $code.Contains($token)) { throw "FAIL R15 lifecycle or instance selection: $token" }
}
foreach ($token in @('Definitions.FirstOrDefault', 'd.RelativePath is null', 'Equals(expected, StringComparison.OrdinalIgnoreCase)',
 'p.Entry.Pid.ToString(CultureInfo.InvariantCulture) == search', 'DefaultIfEmpty(null).Max()')) {
 if (-not $core.Contains($token)) { throw "FAIL R15 component identity or memory: $token" }
}
if ($view.OuterXml.Contains('Tutti i processi rilevati')) { throw 'FAIL R15 ordinary process scope restored.' }
if ($code.Contains('DispatcherTimer') -or $code.Contains('Process.Start(')) { throw 'FAIL R15 unexpected resident work.' }
if (-not $view.SelectSingleNode("//*[@*[local-name()='Name']='SystemInstancesBox']")) { throw 'FAIL R15 missing PID selector.' }
Write-Host 'PASS R15 source: curated components, grouped instances, manual acquisition and explicit RAM maximum.'
