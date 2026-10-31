$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$core = Get-Content -LiteralPath (Join-Path $root 'src\ProcessLens.Core\NativeCaptureEvidence.cs') -Raw -Encoding UTF8
$native = Get-Content -LiteralPath (Join-Path $root 'src\ProcessLens.Windows\WindowsNativeCapture.cs') -Raw -Encoding UTF8
$ui = Get-Content -LiteralPath (Join-Path $root 'src\ProcessLens.App\NativeCaptureView.xaml.cs') -Raw -Encoding UTF8
[xml]$view = Get-Content -LiteralPath (Join-Path $root 'src\ProcessLens.App\NativeCaptureView.xaml') -Raw -Encoding UTF8
foreach ($token in @('0x00010000', 'NativeLibrary.TryGetExport', 'Environment.SpecialFolder.System',
 'BufferSizeMultiplier = 1', 'TruncationSize = NativeCaptureEvidence.SnapshotBytes',
 'PacketMonitorCreateLiveSession', 'PacketMonitorCloseRealtimeStream', 'PacketMonitorCloseSessionHandle',
 'GC.KeepAlive(data)', 'GC.KeepAlive(events)', 'WlanGetNetworkBssList', 'WlanFreeMemory(bss)', 'WlanCloseHandle(client',
 'd.PacketLength > NativeCaptureEvidence.SnapshotBytes', '(ulong)d.PacketOffset + d.PacketLength > d.DataSize')) {
 if (-not $native.Contains($token)) { throw "FAIL R17 native lifetime/ABI/budget: $token" }
}
foreach ($token in @('MaximumRows = 2048', 'SnapshotBytes = 128', 'DurationSeconds = 20',
 'BssEntrySize = 360', 'MaximumBssRows = 512', 'len > 32', 'count > MaximumBssRows')) {
 if (-not $core.Contains($token)) { throw "FAIL R17 capture or WLAN parser: $token" }
}
foreach ($token in @('_operation?.Cancel()', 'generation != _generation', 'NativePacketsTable.ItemsSource = null',
 'WifiBssTable.ItemsSource = null', 'NativeCaptureSourcesBox.SelectedIndex = -1', 'ClipboardText.Copy(this, _report')) {
 if (-not $ui.Contains($token)) { throw "FAIL R17 UI lifetime: $token" }
}
foreach ($forbidden in @('Process.Start(', 'DispatcherTimer', 'WlanSetInterface', 'WlanScan(', 'Socket.Connect', 'HttpClient', 'PacketMonitorSetGlobal')) {
 if (($core + $native + $ui).Contains($forbidden)) { throw "FAIL R17 unexpected operation: $forbidden" }
}
foreach ($button in $view.SelectNodes("//*[local-name()='Button']")) {
 if ($button.HasAttribute('Height') -or $button.GetAttribute('Style') -ne '{StaticResource CaptureAction}') { throw 'FAIL R17 action button.' }
}
foreach ($grid in $view.SelectNodes("//*[local-name()='DataGrid']")) {
 if ($grid.GetAttribute('IsReadOnly') -ne 'True' -or $grid.GetAttribute('EnableRowVirtualization') -ne 'True') { throw 'FAIL R17 table.' }
}
Write-Host 'PASS R17 source: owned native capture, WLAN cache, manual activation and bounded metadata.'
