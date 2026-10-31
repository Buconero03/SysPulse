$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$raw = Get-Content -LiteralPath (Join-Path $root 'src\ProcessLens.Windows\WindowsCompatibleCapture.cs') -Raw -Encoding UTF8
$ui = Get-Content -LiteralPath (Join-Path $root 'src\ProcessLens.App\NativeCaptureView.xaml.cs') -Raw -Encoding UTF8
foreach ($token in @('socket.SetReceiveMode(3)', 'socket.SetReceiveMode(0)', 'socket.Bind(local)',
 'socket.Poll(100000)', 'new byte[65535]', 'rows.Count < NativeCaptureEvidence.MaximumRows',
 'LossCountsAvailable = false', 'HasComponent = false, HasDropReason = false', 'finally { socket?.Dispose(); }')) {
 if (-not $raw.Contains($token)) { throw "FAIL R18 raw capture: $token" }
}
foreach ($token in @('_lastDiagnostics', 'Take(4096)', 'NativeCaptureNoteText.Text = _lastDiagnostics', 'RememberDiagnostics()')) {
 if (-not $ui.Contains($token)) { throw "FAIL R18 diagnostics: $token" }
}
foreach ($forbidden in @('Process.Start(', '.Connect(', '.Send(', 'WlanSetInterface(', 'AuditSetSystemPolicy(')) {
 if (($raw + $ui).Contains($forbidden)) { throw "FAIL R18 unexpected action: $forbidden" }
}
Write-Host 'PASS R18 source: bounded own IPv4 socket, retained error diagnostics, no probes or policy changes.'
