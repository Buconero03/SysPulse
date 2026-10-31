$ErrorActionPreference='Stop'
$root=Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$main=Get-Content (Join-Path $root 'src/ProcessLens.App/MainWindow.xaml.cs') -Raw -Encoding UTF8
$tray=Get-Content (Join-Path $root 'src/ProcessLens.App/TrayWindowController.cs') -Raw -Encoding UTF8
$native=Get-Content (Join-Path $root 'src/ProcessLens.Windows/WindowsDeepProcessInspector.cs') -Raw -Encoding UTF8
foreach($token in @('ProcessIdentitySearch.Matches(row.Process, search, row.FriendlyName)','ProcessGrouping.Build(','new PropertyGroupDescription(nameof(ProcessRow.Group))','foreach (var group in _processGroups) group.Refresh()','RefreshProcessPorts_Click')) {
 if(-not $main.Contains($token)){throw ('FAIL R25 UI wiring: '+$token)}
}
foreach($token in @('_window.WindowState = WindowState.Minimized','IsIconic(hwnd)','_trayActivation.Activate','_trayVersion4 = Shell_NotifyIconW','ChangeWindowMessageFilterEx')) {
 if(-not $tray.Contains($token)){throw ('FAIL R25 tray wiring: '+$token)}
}
foreach($token in @('ProcessGrouping.IdentityMatches','OwnedNetworkTableReader.Read','NetworkAcquisitionComplete = notes.Count == 0','NetworkCapturedUtc = DateTime.UtcNow')) {
 if(-not $native.Contains($token)){throw ('FAIL R25 network wiring: '+$token)}
}
if($native.Contains('ResolveSameExecutableFamily(')){throw 'Name-only network family resolution retained.'}
[xml]$view=Get-Content (Join-Path $root 'src/ProcessLens.App/MainWindow.xaml') -Raw -Encoding UTF8
foreach($name in @('GroupApplicationsCheckBox','ExitApplicationButton','RefreshProcessPortsButton')) {
 if(-not $view.SelectSingleNode("//*[@*[local-name()='Name']='$name']")){throw ('Missing R25 control '+$name)}
}
Write-Host 'PASS R25 source: grouped/flat views, identity filter, native minimize state, one tray callback protocol and bounded owned-PID network tables. Windows shell/runtime verification remains separate.'
