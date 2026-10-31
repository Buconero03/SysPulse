$ErrorActionPreference='Stop'
$root=Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$main=Get-Content (Join-Path $root 'src/ProcessLens.App/MainWindow.xaml.cs') -Raw -Encoding UTF8
$tray=Get-Content (Join-Path $root 'src/ProcessLens.App/TrayWindowController.cs') -Raw -Encoding UTF8
$guard=Get-Content (Join-Path $root 'src/ProcessLens.App/AutomaticViewport.cs') -Raw -Encoding UTF8
foreach($token in @('ObservableCollection<ProcessRow> _visibleRows','ReadingPositionPolicy.Synchronize','old.UpdateFrom(next)','else group.UpdateMembers','if (grouped && item.Group is not null && (!preserveScroll || allowInitialSelection))','_detailViewport?.Begin()','if (_listViewport?.HoldsOrder == true) return;')) {
 if(-not $main.Contains($token)){throw ('FAIL R26 update wiring: '+$token)}
}
if($main.Contains('scrollViewer.ScrollToVerticalOffset(previousVerticalOffset)') -or $main.Contains('ProcessList.ItemsSource = visible')){throw 'R26 must not restore old unguarded offsets or reset ItemsSource.'}
foreach($token in @('root.PreviewMouseWheel','root.PreviewMouseMove','root.PreviewKeyDown','root.PreviewTouchDown','RequestBringIntoViewEvent','_policy.IsCurrent(ticket)','_pending?.Abort()','_root.IsMouseCaptureWithin','FindDataAnchor','DispatcherPriority.ContextIdle','viewer.Content as UIElement','_bringBoundary?.AddHandler')) {
 if(-not $guard.Contains($token)){throw ('FAIL R26 viewport guard: '+$token)}
}
$request=[regex]::Match($tray,'(?s)public void RequestMinimize\(.*?(?=internal void RequestHideToTray|private void Minimize)').Value
if($request.Contains('EnsureTray()') -or -not $request.Contains('_actions.Minimize()')){throw 'R26 minimize must not depend on Explorer icon registration.'}
if($tray.Contains('SystemCommands.MinimizeWindow') -or $tray.Contains('DispatcherPriority.ContextIdle')){throw 'R26 must not queue duplicate system commands or idle minimize work.'}
foreach($token in @('CloseWindow(hwnd)','WindowLifetimePolicy.DeferMinimize','DispatcherPriority.Normal','IsIconic(hwnd)')) {
 if(-not $tray.Contains($token)){throw ('FAIL R26 native window command: '+$token)}
}
[xml]$xaml=Get-Content (Join-Path $root 'src/ProcessLens.App/MainWindow.xaml') -Raw -Encoding UTF8
$list=$xaml.SelectSingleNode("//*[@*[local-name()='Name']='ProcessList']")
if($list.GetAttribute('VirtualizingPanel.ScrollUnit') -ne 'Pixel' -or $list.GetAttribute('VirtualizingPanel.VirtualizationMode') -ne 'Recycling'){throw 'R26 must keep pixel scrolling and virtualized recycling.'}
Write-Host 'PASS R26 source: stable rows/groups, guarded viewports, reading order and direct owned-HWND minimize; Windows rendering/shell verification remains separate.'
