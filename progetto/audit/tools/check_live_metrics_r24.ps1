$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$main = Get-Content -LiteralPath (Join-Path $root 'src/ProcessLens.App/MainWindow.xaml.cs') -Raw -Encoding UTF8
$tray = Get-Content -LiteralPath (Join-Path $root 'src/ProcessLens.App/TrayWindowController.cs') -Raw -Encoding UTF8
$native = Get-Content -LiteralPath (Join-Path $root 'src/ProcessLens.Windows/WindowsLiveCpuSampler.cs') -Raw -Encoding UTF8
foreach ($token in @('MonitorRefreshPolicy.LiveMetricsInterval','ApplyLiveMemorySamples(result.Cpu.Memory)','row.UpdateMemory(RecentMemory(row.Pid))','RefreshMemoryDetails(detail)','_sortMode == "Memoria"','MemorySampledAtMonotonicSeconds = current.MemorySampledAtMonotonicSeconds')) {
    if (-not $main.Contains($token)) { throw ('Missing R24 live-memory wiring: ' + $token) }
}
if (-not $native.Contains('WindowsMemorySampler.Capture(handle)')) { throw 'R24 must reuse the CPU handle for PSAPI.' }
$closing = [regex]::Match($tray, '(?s)private void Closing\(.*?(?=public void RequestMinimize)').Value
if (-not $closing.Contains('e.Cancel = true') -or -not $closing.Contains('RequestMinimize()') -or $closing.Contains('WindowState =')) { throw 'Closing must defer minimize until WPF returns.' }
[xml]$view = Get-Content -LiteralPath (Join-Path $root 'src/ProcessLens.App/MainWindow.xaml') -Raw -Encoding UTF8
$button = $view.SelectSingleNode("//*[@*[local-name()='Name']='MinimizeWindowButton']")
if (-not $button -or $button.GetAttribute('Click') -ne 'MinimizeWindow_Click' -or -not $button.GetAttribute('Style')) { throw 'Missing explicit styled minimize action.' }
$build = Get-Content -LiteralPath (Join-Path $root 'build-dashboard.ps1') -Raw -Encoding UTF8
foreach ($suite in @('LiveMetricsTests','LiveMetricsUiTests')) {
    if (-not $build.Contains('Tests.ProcessLens.' + $suite)) { throw ('Missing Windows build test: ' + $suite) }
}
Write-Host 'PASS R24 source: one-second CPU/RAM wiring, same native handle, timestamp overlay, memory sort and deferred minimize. Windows runtime checks are separate.'
