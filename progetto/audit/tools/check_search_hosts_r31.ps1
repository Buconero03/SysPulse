$ErrorActionPreference='Stop'
$root=Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
function Source([string]$name) { Get-Content (Join-Path $root $name) -Raw -Encoding UTF8 }
$tray=Source 'src/ProcessLens.App/TrayWindowController.cs'
$trace=Source 'src/ProcessLens.App/WindowActionDiagnostics.cs'
$main=Source 'src/ProcessLens.App/MainWindow.xaml.cs'
$xaml=Source 'src/ProcessLens.App/MainWindow.xaml'
$dictionary=Source 'src/ProcessLens.Core/WindowsHostReference.cs'
$ui=Source 'tests/ProcessLens.ProcessGroupsUiTests/Program.cs'
foreach($v in @('SetForegroundWindow(popup.Handle)','_menuOpenQueued || _menu.IsOpen','_minimizeMenuItem.IsEnabled = _window.IsVisible','_diagnostics?.SetOwnerMinimized(true)','_diagnostics?.SetOwnerMinimized(false)')) { if(-not $tray.Contains($v)){throw ('FAIL R31 tray/owner: '+$v)} }
if($tray.Contains('SetForegroundWindow(_hwnd)')){throw 'FAIL R31 context menu must not foreground the minimized main window.'}
foreach($v in @('SetOwnerMinimized','_logWindow.Hide()','_logWindow.ShowActivated = false','logVisibile={LogVisible}','figlieVisibili=')) { if(-not $trace.Contains($v)){throw ('FAIL R31 log child state: '+$v)} }
foreach($v in @('FocusSearch','ApplicationCommands.Find','Key.F','WindowsHostReference.Matches','Nessun risultato','hostReference.Title')) { if(-not $main.Contains($v)){throw ('FAIL R31 search/reference: '+$v)} }
foreach($v in @('Name="SearchSurface"','MouseLeftButtonDown="SearchSurface_MouseLeftButtonDown"','CaretBrush="#175CD3"','Cerca per nome visualizzato')) { if(-not $xaml.Contains($v)){throw ('FAIL R31 search editor: '+$v)} }
foreach($v in @('prevhost.exe','RuntimeBroker.exe','smartscreen.exe','svchost.exe','StringComparison.OrdinalIgnoreCase','text.All(char.IsAsciiDigit)','ServiceDisplayNames.Take(2)')) { if(-not $dictionary.Contains($v)){throw ('FAIL R31 local references: '+$v)} }
foreach($v in @('R31 shown search field receives keyboard focus','R31 minimizing also hides','R32 right-click menu preserves taskbar minimize','R31 Show from tray restores','R31 search finds the visible dictionary title')) { if(-not $ui.Contains($v)){throw ('FAIL R31 owned UI fixture: '+$v)} }
Write-Host 'PASS R31 source: focused search and visible editor, local Microsoft host references with exact-name boundaries, popup-only menu activation and owned-log minimize/restore. Actual WPF/shell checks remain Windows-only.'
