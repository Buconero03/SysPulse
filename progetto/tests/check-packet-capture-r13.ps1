$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$client = Get-Content (Join-Path $root 'src\ProcessLens.Windows\WiresharkTools.cs') -Raw -Encoding UTF8
$policy = Get-Content (Join-Path $root 'src\ProcessLens.Core\PacketCaptureEvidence.cs') -Raw -Encoding UTF8
$ui = Get-Content (Join-Path $root 'src\ProcessLens.App\NetworkCaptureView.xaml.cs') -Raw -Encoding UTF8
[xml]$view = Get-Content (Join-Path $root 'src\ProcessLens.App\NetworkCaptureView.xaml') -Raw -Encoding UTF8
foreach ($token in @('UseShellExecute = false','child.StartInfo.ArgumentList.Add(argument)', 'budget.CancelAfter',
    'PacketCapturePolicy.MaximumTextChars', 'if (started)', 'child.Kill()', 'FileMode.CreateNew',
    '_verifiedMonitor.Contains(Stamp(tools, adapter))', 'FileShare.Read', 'SHA256.HashDataAsync')) {
    if (-not $client.Contains($token)) { throw "FAIL R13 capture guard: $token" }
}
foreach ($token in @('duration:20','filesize:10000','"-c", "10000"','"-s", "256"','"-n", "-r"','"-c", "2048"',
    'LocalInterface(adapter.Name)', 'Rete annunciata', 'Destinatario nel frame')) {
    if (-not $policy.Contains($token)) { throw "FAIL R13 packet policy: $token" }
}
foreach ($token in @('_operation?.Cancel()', 'generation == _generation', 'CaptureMonitorBox.IsChecked != true || _monitorVerified',
    'CaptureInterfacesBox.SelectedIndex = -1', 'ClipboardText.Copy(this,')) {
    if (-not $ui.Contains($token)) { throw "FAIL R13 UI lifecycle: $token" }
}
foreach ($button in $view.SelectNodes("//*[local-name()='Button']")) {
    if ($button.HasAttribute('Height') -or $button.GetAttribute('Style') -ne '{StaticResource CaptureAction}') {
        throw 'FAIL R13: unstyled or fixed-height capture button.'
    }
}
foreach ($forbidden in @('GetProcessesByName(', 'GetProcessById(', 'cmd.exe', 'powershell.exe', 'msiexec', 'schtasks')) {
    if (($client + $policy + $ui).Contains($forbidden)) { throw "FAIL R13 unexpected action: $forbidden" }
}
Write-Host 'PASS R13: explicit capture, bounded commands, local interface, monitor verification, owned-child cleanup, copied output. Source checks only.'
