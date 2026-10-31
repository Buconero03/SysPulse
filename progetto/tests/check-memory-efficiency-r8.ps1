$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$ui = Get-Content -LiteralPath (Join-Path $root 'src\ProcessLens.App\MainWindow.xaml.cs') -Raw -Encoding UTF8
[xml]$view = Get-Content -LiteralPath (Join-Path $root 'src\ProcessLens.App\MainWindow.xaml') -Raw -Encoding UTF8
foreach ($name in @('GuardianDashboardHost','MemoryDashboardHost')) {
    if (-not $view.SelectSingleNode("//*[@*[local-name()='Name']='$name' and local-name()='ContentControl']")) { throw ('Missing lazy dashboard host: ' + $name) }
}
if ($view.SelectNodes("//*[local-name()='GuardianDashboardView' or local-name()='MemoryDashboardView']").Count -ne 0) { throw 'Dashboard is still eagerly constructed in XAML.' }
foreach ($token in @('guardian && GuardianDashboard is null','memory && MemoryDashboard is null','GuardianDashboard?.SetActive(guardian)','MemoryDashboard?.SetActive(memory)',
    'DetailIdentification_Expanded','DetailIdentification_Collapsed','DetailTechnicalSections_Collapsed','_currentDetailRow = null;',
    'MemoryObservationBuffer Samples','state.Samples.Add(now, bytes)')) {
    if (-not $ui.Contains($token)) { throw ('Missing memory/lifecycle guard: ' + $token) }
}
foreach ($name in @('ProcessList','ServiceList')) {
    $node = $view.SelectSingleNode("//*[@*[local-name()='Name']='$name']")
    if ($node.GetAttribute('VirtualizingPanel.VirtualizationMode') -ne 'Recycling' -or
        $node.GetAttribute('ScrollViewer.CanContentScroll') -ne 'True') { throw ('Missing recycling: ' + $name) }
}
Write-Host 'PASS R8: lazy dashboard hosts, on-demand details, sample buffer, recycling and lifecycle guards.'
