$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$core = Get-Content -LiteralPath (Join-Path $root 'src\ProcessLens.Core\NativeNetworkEvidence.cs') -Raw -Encoding UTF8
$native = Get-Content -LiteralPath (Join-Path $root 'src\ProcessLens.Windows\WindowsNativeNetworkEvidence.cs') -Raw -Encoding UTF8
$ui = Get-Content -LiteralPath (Join-Path $root 'src\ProcessLens.App\NativeNetworkEvidenceView.xaml.cs') -Raw -Encoding UTF8
[xml]$view = Get-Content -LiteralPath (Join-Path $root 'src\ProcessLens.App\NativeNetworkEvidenceView.xaml') -Raw -Encoding UTF8
foreach ($token in @('EvtQuery(IntPtr.Zero, "Security"', 'EvtNext(query, 1, events, 250, 0',
 'AuditQuerySystemPolicy(ref category, 1', 'finally { if (policy != IntPtr.Zero) AuditFree(policy); }',
 'finally { if (query != IntPtr.Zero) _api.Close(query); }', 'finally { if (record != IntPtr.Zero) _api.Close(record); }',
 'finally { Marshal.FreeHGlobal(buffer); }', 'TimeSpan.FromSeconds(3)', 'MaximumTotalXmlChars - chars', 'if (error == 259)')) {
 if (-not $native.Contains($token)) { throw "FAIL R16 native evidence lifetime/budget: $token" }
}
foreach ($token in @('DtdProcessing = DtdProcessing.Prohibit','XmlResolver = null','MaximumEvents = 256',
 'MaximumXmlChars = 32768', 'conteggi N/D', 'LogonType', 'logon is not ("3" or "10")')) {
 if (-not $core.Contains($token)) { throw "FAIL R16 parser or observation boundary: $token" }
}
foreach ($forbidden in @('Process.Start(', 'AuditSetSystemPolicy(', 'FwpmFilterAdd', 'EvtSubscribe', 'AdjustTokenPrivileges',
 'WlanSet', 'DispatcherTimer', 'Socket.Connect', 'HttpClient', 'Task.Run(')) {
 if (($native + $core).Contains($forbidden)) { throw "FAIL R16 unexpected native operation: $forbidden" }
}
foreach ($token in @('_operation?.Cancel()', 'generation != _generation', 'NativeNetworkEventsTable.ItemsSource = null;', 'ClipboardText.Copy(this,')) {
 if (-not $ui.Contains($token)) { throw "FAIL R16 UI lifetime: $token" }
}
foreach ($button in $view.SelectNodes("//*[local-name()='Button']")) {
 if ($button.HasAttribute('Height') -or $button.GetAttribute('Style') -ne '{StaticResource NativeAction}') { throw 'FAIL R16 native button wrapping.' }
}
Write-Host 'PASS R16 source: local read-only Windows evidence, audit status, bounded history and owned resource cleanup.'
