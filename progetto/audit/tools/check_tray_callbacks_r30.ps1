$ErrorActionPreference='Stop'
$root=Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
function Source([string]$name) { Get-Content (Join-Path $root $name) -Raw -Encoding UTF8 }
$tray=Source 'src/ProcessLens.App/TrayWindowController.cs'
$router=Source 'src/ProcessLens.App/TrayCallbackRouter.cs'
$trace=Source 'src/ProcessLens.App/WindowActionDiagnostics.cs'
$ui=Source 'tests/ProcessLens.ProcessGroupsUiTests/Program.cs'
foreach($value in @('TrayCallbackRouter.Decode(wParam.ToInt64(), lParam.ToInt64(), 1)','TRAY_CALLBACK','TrayCallbackRouter.IsActivation(packet)','TrayCallbackRouter.IsContextMenu(packet)','TRAY_DECISION','duplicato ignorato','_trayActivation.Activate','TRAY_FILTER','TRAY_REGISTER','BeginTrayAttempt')) {
 if(-not $tray.Contains($value)){throw ('FAIL R30 tray callback: '+$value)}
}
if($tray.Contains('_trayVersion4 ? notification')){throw 'FAIL R30 negotiated version must not discard a left-button callback.'}
if($tray.IndexOf('"TRAY_CALLBACK"') -gt $tray.IndexOf('if (TrayCallbackRouter.IsActivation(packet))')){throw 'FAIL R30 raw callback must be logged before filtering.'}
foreach($value in @('bits & 0xFFFF','bits >> 16','packedId == expectedIconId','wParam == expectedIconId','0x0202 or 0x0203 or 0x0400 or 0x0401','0x0205 or 0x007B')) {
 if(-not $router.Contains($value)){throw ('FAIL R30 packet decode/identity: '+$value)}
}
foreach($value in @('BeginTrayAttempt','Begin("TRAY_INPUT", detail)','Snapshot("TRAY_BEFORE")')) {
 if(-not $trace.Contains($value)){throw ('FAIL R30 independent tray attempt trace: '+$value)}
}
foreach($value in @('void Tray30(','0x8000+69','actual HWND v4 mouse release','actual HWND later select/up','actual HWND legacy-layout','actual HWND keyboard activation','actual HWND wrong icon')) {
 if(-not $ui.Contains($value)){throw ('FAIL R30 owned-HWND fixture: '+$value)}
}
Write-Host 'PASS R30 source: both Shell packet layouts, left release/select/key activation, exact icon ID, duplicate gate, raw callback diagnostics and owned-HWND Windows fixture. Physical tray clicks remain separate.'
