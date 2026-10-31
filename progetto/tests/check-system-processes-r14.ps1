$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
[xml]$main = Get-Content -LiteralPath (Join-Path $root 'src\ProcessLens.App\MainWindow.xaml') -Raw -Encoding UTF8
[xml]$view = Get-Content -LiteralPath (Join-Path $root 'src\ProcessLens.App\SystemProcessesView.xaml') -Raw -Encoding UTF8
$code = Get-Content -LiteralPath (Join-Path $root 'src\ProcessLens.App\SystemProcessesView.xaml.cs') -Raw -Encoding UTF8
$native = Get-Content -LiteralPath (Join-Path $root 'src\ProcessLens.Windows\WindowsSystemProcessSource.cs') -Raw -Encoding UTF8
foreach ($name in @('NavSystemButton','SystemProcessesHost')) {
 if (-not $main.SelectSingleNode("//*[@*[local-name()='Name']='$name']")) { throw "FAIL R14 missing navigation: $name" }
}
if ($main.SelectNodes("//*[local-name()='SystemProcessesView']").Count -ne 0) { throw 'FAIL R14 eager native view.' }
foreach ($token in @('MaximumBuffer = 16 * 1024 * 1024','MaximumRows = 16384',
 'finally { Marshal.FreeHGlobal(buffer); }','OpenProcess(0x1000, false', 'if (start > began)',
 'NTSTATUS 0x{status:X8}', 'error != 18', 'nameOffset > end - n.Length')) {
 if (-not $native.Contains($token)) { throw "FAIL R14 native guard: $token" }
}
foreach ($token in @('if (!_active || _busy) return;', 'generation != _generation',
 '_operation?.Cancel()', 'SystemProcessesTable.ItemsSource = null;', 'ClipboardText.Copy(this,')) {
 if (-not $code.Contains($token)) { throw "FAIL R14 lifecycle: $token" }
}
foreach ($forbidden in @('PROCESS_VM_READ', 'Process.Start(', '.Kill(', 'AdjustTokenPrivileges(', 'DispatcherTimer')) {
 if (($native + $code).Contains($forbidden)) { throw "FAIL R14 unexpected operation: $forbidden" }
}
foreach ($button in $view.SelectNodes("//*[local-name()='Button']")) {
 if ($button.HasAttribute('Height') -or $button.GetAttribute('Style') -ne '{StaticResource SystemAction}') {
  throw 'FAIL R14 button spacing or wrapping style.'
 }
}
Write-Host 'PASS R14 source only: lazy system inventory, bounded read-only acquisition, identity guards and copy.'
