$ErrorActionPreference='Stop'
$root=Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$main=Get-Content (Join-Path $root 'src/ProcessLens.App/MainWindow.xaml.cs') -Raw -Encoding UTF8
$tray=Get-Content (Join-Path $root 'src/ProcessLens.App/TrayWindowController.cs') -Raw -Encoding UTF8
$icon=Get-Content (Join-Path $root 'src/ProcessLens.App/ProcessIconResolver.cs') -Raw -Encoding UTF8
$policy=Get-Content (Join-Path $root 'src/ProcessLens.App/WindowLifetimePolicy.cs') -Raw -Encoding UTF8
$build=Get-Content (Join-Path $root 'build-dashboard.ps1') -Raw -Encoding UTF8
$fixture=Get-Content (Join-Path $root 'tests/ProcessLens.ProcessGroupsUiTests/Program.cs') -Raw -Encoding UTF8
$click=[regex]::Match($main,'(?s)private void MinimizeWindow_Click\(.*?(?=private void NewTask)').Value
if(-not $click){throw 'R28 minimize handler not found.'}
if($click.Contains('AppDiagnostics.Record(') -or $click.Contains('Dispatcher.BeginInvoke')){throw 'R28 ordinary header click must not perform synchronous file logging or defer the state change.'}
foreach($token in @('DeferMinimize(_closing, _nativeCallbackDepth > 0)','_actions.MinimizeImmediately()','new WindowInteropHelper(_window).Handle','CloseWindow(hwnd)','QueueActivityUpdate()','DispatcherPriority.Background','RecordWindowAction')) {
 if(-not $tray.Contains($token)){throw ('FAIL R28 window wiring: '+$token)}
}
if($tray.Contains('WindowLifetimePolicy.IsMinimizeMessage') -or $tray.Contains('message == 0x0112') -or $tray.Contains('message == 0x0010')){throw 'R28 native caption messages must pass through to Windows/WPF.'}
if(-not $policy.Contains('_pending = null; // A queued restore must not undo this explicit click.')){throw 'R28 old queued restore must be cancelled.'}
foreach($token in @('NativeIconPreference.ResolveExecutable','GetResolution(process.ExecutablePath, process.Name)','IsGenericIcon','IconPreferencePolicy.SamePixels','new(128, StringComparer.OrdinalIgnoreCase)')) {
 if(-not $icon.Contains($token)){throw ('FAIL R28 original icon priority: '+$token)}
}
if(-not $build.Contains('Tests.ProcessLens.MainWindowShell') -or -not $build.Contains('--window-smoke')){throw 'R28 real MainWindow shell fixture missing from Windows build gate.'}
foreach($token in @('static void MainWindowSmoke()','new MainWindow(false)','main.SetDashboardWindowVisibility(active)','IsIconic(Handle())','MinimizeWindowButton','SendMessageW(Handle(),0x112')) {
 if(-not $fixture.Contains($token)){throw ('FAIL R28 owned MainWindow fixture: '+$token)}
}
Write-Host 'PASS R28 source: native icons first, Windows generic fallback, immediate header action, native caption pass-through, deferred bounded logging/activity and owned MainWindow Windows build gate. WPF execution is separate.'
