$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
[xml]$main = Get-Content -LiteralPath (Join-Path $root 'src\ProcessLens.App\MainWindow.xaml') -Raw -Encoding UTF8
[xml]$view = Get-Content -LiteralPath (Join-Path $root 'src\ProcessLens.App\NetworkDevicesView.xaml') -Raw -Encoding UTF8
$code = Get-Content -LiteralPath (Join-Path $root 'src\ProcessLens.App\NetworkDevicesView.xaml.cs') -Raw -Encoding UTF8
$native = Get-Content -LiteralPath (Join-Path $root 'src\ProcessLens.Windows\WindowsNetworkDevices.cs') -Raw -Encoding UTF8
$core = Get-Content -LiteralPath (Join-Path $root 'src\ProcessLens.Core\NetworkDevices.cs') -Raw -Encoding UTF8
foreach ($name in @('NavNetworkButton','NetworkDevicesHost')) {
    if (-not $main.SelectSingleNode("//*[@*[local-name()='Name']='$name']")) { throw "FAIL R12 network navigation: $name" }
}
if ($main.SelectNodes("//*[local-name()='NetworkDevicesView']").Count -ne 0) { throw 'FAIL R12: network view eagerly constructed.' }
foreach ($token in @('GetIpNetTable2(0, out table)', 'finally { if (table != IntPtr.Zero) FreeMibTable(table); }',
    'Marshal.OffsetOf<NativeHeader>', 'Marshal.SizeOf<NativeRow>()', 'MaximumRows = 4096', 'changed ? ""')) {
    if (-not $native.Contains($token)) { throw "FAIL R12 native guard: $token" }
}
foreach ($token in @('if (!_active || _busy) return;', 'generation != _generation', 'NetworkDevicesTable.ItemsSource = null;',
    '_snapshot.Available && !_snapshot.Partial', 'ClipboardText.Copy(this,', 'new WindowsNetworkDevices()')) {
    if (-not $code.Contains($token)) { throw "FAIL R12 lifecycle or copy: $token" }
}
foreach ($forbidden in @('Process.Start(', 'DispatcherTimer', 'Dns.Get', 'new Ping(', 'ResolveIpNetEntry2(', 'SetIpNetEntry2(')) {
    if (($native + $code).Contains($forbidden)) { throw "FAIL R12: unexpected active operation $forbidden" }
}
$grid = $view.SelectSingleNode("//*[@*[local-name()='Name']='NetworkDevicesTable']")
if ($grid.GetAttribute('IsReadOnly') -ne 'True' -or $grid.GetAttribute('GridCopySupport.Enabled', 'clr-namespace:ProcessLens.App') -ne 'True') {
    throw 'FAIL R12: read-only or copied network table missing.'
}
foreach ($button in $view.SelectNodes("//*[local-name()='Button']")) {
    if ($button.HasAttribute('Height') -or $button.GetAttribute('Style') -ne '{StaticResource NetworkAction}') {
        throw 'FAIL R12: unstyled or fixed-height network button.'
    }
}
foreach ($token in @('capture.Partial ||', '"#64748B"', 'MaximumDevices = 512', 'MaximumBytes = 524288',
    'if (!existing.Available) return existing.Error;', 'File.Move(temporary, _path, true)', 'current.Count > 1024')) {
    if (-not $core.Contains($token)) { throw "FAIL R12 interpretation or persistence: $token" }
}
if (-not $core.Contains('ex is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or ArgumentException')) {
    throw 'FAIL R14 R1: invalid catalog format or oversized file must return an unavailable catalog.'
}
$tabs = $view.SelectSingleNode("//*[@*[local-name()='Name']='NetworkTabs']")
if ($null -eq $tabs -or $tabs.GetAttribute('SelectedIndex') -ne '0') {
    throw 'FAIL R14 R2: cache tab must be selected explicitly before Loaded.'
}
Write-Host 'PASS R12: passive network view, manual refresh, scoped labels, incomplete-data states and copy. Source checks only.'
