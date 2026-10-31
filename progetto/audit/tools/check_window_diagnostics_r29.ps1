$ErrorActionPreference='Stop'
$root=Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
function Read-Source([string]$file) { Get-Content (Join-Path $root $file) -Raw -Encoding UTF8 }
$trace=Read-Source 'src/ProcessLens.App/WindowActionDiagnostics.cs'
$journal=Read-Source 'src/ProcessLens.App/WindowDiagnosticJournal.cs'
$log=Read-Source 'src/ProcessLens.App/AppDiagnostics.cs'
$icon=Read-Source 'src/ProcessLens.App/ProcessIconResolver.cs'
$main=Read-Source 'src/ProcessLens.App/MainWindow.xaml.cs'
$app=Read-Source 'src/ProcessLens.App/App.xaml.cs'
$tray=Read-Source 'src/ProcessLens.App/TrayWindowController.cs'
$ui=Read-Source 'tests/ProcessLens.ProcessGroupsUiTests/Program.cs'
foreach($value in @('PreviewMouseDownEvent, _mouse, true','PreviewKeyDownEvent, _key, true','UI_CHECK_QUEUED','Timeout.Infinite','_probe.Change(2000','_probe.Dispose()','_window.RemoveHandler','Snapshot("STATE")','SC_MINIMIZE','log.Show()','CreateLogContent()','IsReadOnly = true','ClipboardText.Copy','AppDiagnostics.WindowLogFile')) {
 if(-not $trace.Contains($value)){throw ('FAIL R29 diagnostic trace: '+$value)}
}
if($trace.Contains('ShowDialog(') -or $trace.Contains('e.Handled =') -or $trace.Contains('Process.GetProcesses')){throw 'R29 diagnostics must observe without modal blocking, input suppression or process acquisition.'}
foreach($value in @('Math.Clamp(capacity, 1, 256)','value.Length > 1400','_utc():O','_mono() - _start','_lines.Dequeue()')) {
 if(-not $journal.Contains($value)){throw ('FAIL R29 bounded journal: '+$value)}
}
foreach($value in @('window-actions.log','window-actions.previous.log','capacity: 256','1024 * 1024','Coda piena')) {
 if(-not $log.Contains($value)){throw ('FAIL R29 bounded disk trace: '+$value)}
}
foreach($value in @('NativeIconPreference.ResolveExecutable','TryGetResourceIcon','ExtractIconExW(path, 0, out icon, IntPtr.Zero, 1)','count is 0 or uint.MaxValue','if (icon != 0) DestroyIcon(icon)','IsLocalExecutable','Cache[key] = result')) {
 if(-not $icon.Contains($value)){throw ('FAIL R29 original icon fallback: '+$value)}
}
if(-not $main.Contains('WindowDiagnostics.Click()') -or -not $app.Contains('window.WindowDiagnostics') -or -not $tray.Contains('_diagnostics?.NativeMessage(message, wParam.ToInt64())')){throw 'R29 real main window/controller trace not connected.'}
foreach($value in @('main.WindowDiagnostics','R29 log view is modeless','R29 actual minimize records','R29 native title-bar minimize','CreateLogContent()')) {
 if(-not $ui.Contains($value)){throw ('FAIL R29 WPF fixture: '+$value)}
}
Write-Host 'PASS R29 source: observed input/click/native/state/restore, independent delayed dispatcher probe, bounded local log and modeless copy view; original Shell icons and lazy executable resource fallback. WPF execution remains separate.'
