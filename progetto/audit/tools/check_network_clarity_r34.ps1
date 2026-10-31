$ErrorActionPreference='Stop'
$root=Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
function Source([string]$name) { Get-Content (Join-Path $root $name) -Raw -Encoding UTF8 }
$devices=Source 'src/ProcessLens.App/NetworkDevicesView.xaml.cs'
$events=Source 'src/ProcessLens.App/NativeNetworkEvidenceView.xaml.cs'
$capture=Source 'src/ProcessLens.App/NativeCaptureView.xaml.cs'
$native=Source 'src/ProcessLens.Windows/WindowsNativeNetworkEvidence.cs'
$presentation=Source 'src/ProcessLens.Core/NetworkReadPresentation.cs'
foreach($token in @('ResetCacheView','Nessuna lettura corrente','NetworkEmptyBorder.Visibility','NetworkReadPresentation.DeviceCounts','NetworkJournalText.Text = _journal.Report')) {
 if(-not $devices.Contains($token)){throw ('FAIL R34 cache state/log: '+$token)}
}
foreach($token in @('INativeNetworkEvidenceQuerySource','NativeEventWindowBox','NativePacketDropsBox','NetworkReadPresentation.EventCounts','NativeEventEmptyBorder.Visibility')) {
 if(-not $events.Contains($token)){throw ('FAIL R34 event controls: '+$token)}
}
foreach($token in @('EventID=5155','EventID=5159','options.IncludePacketDrops ? " or EventID=5152"','ScannedEvents = scanned','MalformedEvents = malformed','MaximumTotalXmlChars - chars')) {
 if(-not $native.Contains($token)){throw ('FAIL R34 native scope/budget: '+$token)}
}
foreach($token in @('Capacity = 128','MaximumMessageChars = 1200','_entries.Dequeue()','ToUniversalTime():O','char.IsControl(c)','DeviceEmpty','PacketDetails','WifiDetails')) {
 if(-not $presentation.Contains($token)){throw ('FAIL R34 presentation/log boundary: '+$token)}
}
foreach($token in @('NetworkReadPresentation.PacketDetails','NetworkReadPresentation.WifiDetails','_journal.Add(operationName, "ESITO"','_operation?.Cancel()')) {
 if(-not $capture.Contains($token)){throw ('FAIL R34 capture explanations/lifecycle: '+$token)}
}
foreach($forbidden in @('DispatcherTimer','AuditSetSystemPolicy(','EvtSubscribe(','new Ping(','Dns.Get','WlanScan(','Process.Start(')) {
 if(($devices+$events+$capture+$native+$presentation).Contains($forbidden)){throw ('FAIL R34 unexpected background/probe/policy action: '+$forbidden)}
}
foreach($name in @('NetworkDevicesView','NativeNetworkEvidenceView','NativeCaptureView')) {
 [xml]$x=Source ('src/ProcessLens.App/'+$name+'.xaml')
 $names=@($x.SelectNodes('//*[@*[local-name()="Name"]]') | ForEach-Object { $_.GetAttribute('Name','http://schemas.microsoft.com/winfx/2006/xaml') })
 if(@($names|Select-Object -Unique).Count -ne $names.Count){throw ('FAIL R34 duplicate XAML name: '+$name)}
 foreach($grid in $x.SelectNodes('//*[local-name()="DataGrid"]')) {
  if($grid.GetAttribute('IsReadOnly') -ne 'True' -or $grid.GetAttribute('EnableRowVirtualization') -ne 'True'){throw ('FAIL R34 table mutability/virtualization: '+$name)}
 }
}
Write-Host 'PASS R34 network: explicit empty/freshness states, bounded session log, explained observations, optional bounded packet-drop read and manual lifecycle. WPF runtime remains Windows-only.'
