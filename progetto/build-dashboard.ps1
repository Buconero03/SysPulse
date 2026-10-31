$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
. (Join-Path $PSScriptRoot 'build-diagnostics.ps1')
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'Installare .NET 8 SDK prima di compilare ProcessLens Dashboard.'
}
Write-BuildDiagnostic -EventId 'PL-BUILD-5001' -Stage 'BuildDashboard' -Result 'Started' -Severity 'Information'
Write-Host '[FASE 1] Controlli statici WPF, accesso SQLite in sola lettura e sicurezza del profiler...' -ForegroundColor Cyan
Write-Host 'Controllo sintassi di tutti gli script PowerShell...' -ForegroundColor Cyan
& (Join-Path $PSScriptRoot 'tests\check-powershell-syntax.ps1')
& (Join-Path $PSScriptRoot 'tests\check-wpf-contextmenu.ps1')
& (Join-Path $PSScriptRoot 'tests\check-ui-readability.ps1')
& (Join-Path $PSScriptRoot 'tests\check-memory-efficiency-r8.ps1')
& (Join-Path $PSScriptRoot 'tests\check-ui-format-r9.ps1')
& (Join-Path $PSScriptRoot 'tests\check-workspace-r10r10.ps1')
& (Join-Path $PSScriptRoot 'tests\check-ux-writing-r10r11.ps1')
& (Join-Path $PSScriptRoot 'tests\check-network-devices-r12.ps1')
& (Join-Path $PSScriptRoot 'tests\check-native-network-r16.ps1')
& (Join-Path $PSScriptRoot 'tests\check-native-capture-r17.ps1')
& (Join-Path $PSScriptRoot 'tests\check-network-recovery-r18.ps1')
& (Join-Path $PSScriptRoot 'tests\check-copy-layout-r18r1.ps1')
& (Join-Path $PSScriptRoot 'tests\check-packet-capture-r13.ps1')
& (Join-Path $PSScriptRoot 'tests\check-system-processes-r14.ps1')
& (Join-Path $PSScriptRoot 'tests\check-windows-components-r15.ps1')
& (Join-Path $PSScriptRoot 'tests\check-wpf-test-context-r14r3.ps1')
& (Join-Path $PSScriptRoot 'tests\check-copy-ui-r10.ps1')
& (Join-Path $PSScriptRoot 'tests\check-terminology-ui.ps1')
& (Join-Path $PSScriptRoot 'tests\check-detail-hierarchy-v01910.ps1')
& (Join-Path $PSScriptRoot 'tests\check-classification-language-v01911.ps1')
& (Join-Path $PSScriptRoot 'tests\check-classification-hierarchy-v01914.ps1')
& (Join-Path $PSScriptRoot 'tests\check-guardian-dashboard.ps1')
& (Join-Path $PSScriptRoot 'tests\check-memory-dashboard.ps1')
& (Join-Path $PSScriptRoot 'tests\check-memory-ui-v0195.ps1')
& (Join-Path $PSScriptRoot 'tests\check-structured-queries.ps1')
& (Join-Path $PSScriptRoot 'tests\check-memory-measurement-audit.ps1')
& (Join-Path $PSScriptRoot 'tests\check-windows-allocation-inspector.ps1')
& (Join-Path $PSScriptRoot 'tests\check-profiler-isolation.ps1')
& (Join-Path $PSScriptRoot 'tests\check-profiler-fixture-v01912.ps1')
& (Join-Path $PSScriptRoot 'tests\check-diagnostics-v01913.ps1')
& (Join-Path $PSScriptRoot 'audit\tools\check_live_metrics_r24.ps1')
& (Join-Path $PSScriptRoot 'audit\tools\check_process_groups_r25.ps1')
& (Join-Path $PSScriptRoot 'audit\tools\check_scroll_minimize_r26.ps1')
& (Join-Path $PSScriptRoot 'audit\tools\check_windows_icons_r27.ps1')
& (Join-Path $PSScriptRoot 'audit\tools\check_window_controls_r28.ps1')
& (Join-Path $PSScriptRoot 'audit\tools\check_window_diagnostics_r29.ps1')
& (Join-Path $PSScriptRoot 'audit\tools\check_tray_callbacks_r30.ps1')
& (Join-Path $PSScriptRoot 'audit\tools\check_search_hosts_r31.ps1')
& (Join-Path $PSScriptRoot 'audit\tools\check_build_reference_r31r1.ps1')
& (Join-Path $PSScriptRoot 'audit\tools\check_notification_area_r32.ps1')
& (Join-Path $PSScriptRoot 'audit\tools\check_wpf_path_r32r1.ps1')
& (Join-Path $PSScriptRoot 'audit\tools\check_embedded_icon_r32r2.ps1')
& (Join-Path $PSScriptRoot 'audit\tools\check_process_symbols_r33.ps1')
& (Join-Path $PSScriptRoot 'audit\tools\check_network_clarity_r34.ps1')
Write-Host 'Test catalogo eventi, struttura JSONL e privacy...' -ForegroundColor Cyan
Invoke-CheckedDotnet -Stage 'Tests.ProcessLens.DiagnosticsTests' -Project '.\tests\ProcessLens.DiagnosticsTests\ProcessLens.DiagnosticsTests.csproj' -Arguments @('run','--project','.\tests\ProcessLens.DiagnosticsTests\ProcessLens.DiagnosticsTests.csproj','-c','Release')
Write-Host 'Test del lettore SQLite (solo lettura, dati sintetici)...' -ForegroundColor Cyan
Invoke-CheckedDotnet -Stage 'Tests.ProcessLens.GuardianDashboardTests' -Project '.\tests\ProcessLens.GuardianDashboardTests\ProcessLens.GuardianDashboardTests.csproj' -Arguments @('run', '--project', '.\tests\ProcessLens.GuardianDashboardTests\ProcessLens.GuardianDashboardTests.csproj', '-c', 'Release')
Write-Host 'Test Memory Alert Policy 85/80...' -ForegroundColor Cyan
Invoke-CheckedDotnet -Stage 'Tests.ProcessLens.MemoryAlertTests' -Project '.\tests\ProcessLens.MemoryAlertTests\ProcessLens.MemoryAlertTests.csproj' -Arguments @('run', '--project', '.\tests\ProcessLens.MemoryAlertTests\ProcessLens.MemoryAlertTests.csproj', '-c', 'Release')
Write-Host 'Test CPU: intervalli monotoni, identita e ripresa...' -ForegroundColor Cyan
Invoke-CheckedDotnet -Stage 'Tests.ProcessLens.CpuTests' -Project '.\tests\ProcessLens.CpuTests\ProcessLens.CpuTests.csproj' -Arguments @('run','--project','.\tests\ProcessLens.CpuTests\ProcessLens.CpuTests.csproj','-c','Release')
Write-Host 'Test R24: RAM live, identita, letture PSAPI simulate e riduzione differita...' -ForegroundColor Cyan
Invoke-CheckedDotnet -Stage 'Tests.ProcessLens.LiveMetricsTests' -Project '.\tests\ProcessLens.LiveMetricsTests\ProcessLens.LiveMetricsTests.csproj' -Arguments @('run','--project','.\tests\ProcessLens.LiveMetricsTests\ProcessLens.LiveMetricsTests.csproj','-c','Release')
Write-Host 'Test WPF R24: righe RAM e comando Riduci a icona con fixture...' -ForegroundColor Cyan
Invoke-CheckedDotnet -Stage 'Tests.ProcessLens.LiveMetricsUiTests' -Project '.\tests\ProcessLens.LiveMetricsUiTests\ProcessLens.LiveMetricsUiTests.csproj' -Arguments @('run','--project','.\tests\ProcessLens.LiveMetricsUiTests\ProcessLens.LiveMetricsUiTests.csproj','-c','Release')
Write-Host 'Test R25: gruppi, filtro identita e lettore tabelle porte...' -ForegroundColor Cyan
Invoke-CheckedDotnet -Stage 'Tests.ProcessLens.ProcessGroupsTests' -Project '.\tests\ProcessLens.ProcessGroupsTests\ProcessLens.ProcessGroupsTests.csproj' -Arguments @('run','--project','.\tests\ProcessLens.ProcessGroupsTests\ProcessLens.ProcessGroupsTests.csproj','-c','Release')
Write-Host 'Test WPF R25: gruppi e filtro con fixture, nessuna acquisizione reale...' -ForegroundColor Cyan
Invoke-CheckedDotnet -Stage 'Tests.ProcessLens.ProcessGroupsUiTests' -Project '.\tests\ProcessLens.ProcessGroupsUiTests\ProcessLens.ProcessGroupsUiTests.csproj' -Arguments @('run','--project','.\tests\ProcessLens.ProcessGroupsUiTests\ProcessLens.ProcessGroupsUiTests.csproj','-c','Release')
Write-Host 'Test R30 finestra reale: callback icona orologio, pulsante Riduci, riapertura e uscita; dati simulati...' -ForegroundColor Cyan
Invoke-CheckedDotnet -Stage 'Tests.ProcessLens.MainWindowShell' -Project '.\tests\ProcessLens.ProcessGroupsUiTests\ProcessLens.ProcessGroupsUiTests.csproj' -Arguments @('run','--project','.\tests\ProcessLens.ProcessGroupsUiTests\ProcessLens.ProcessGroupsUiTests.csproj','-c','Release','--','--window-smoke')
Write-Host 'Test icona incorporata: risorsa assembly e formato ICO, nessuna finestra...' -ForegroundColor Cyan
Invoke-CheckedDotnet -Stage 'Tests.ProcessLens.IconResourceTests' -Project '.\tests\ProcessLens.IconResourceTests\ProcessLens.IconResourceTests.csproj' -Arguments @('run','--project','.\tests\ProcessLens.IconResourceTests\ProcessLens.IconResourceTests.csproj','-c','Release')
Write-Host '[FASE 2] Test RAM: separazione memoria fisica/virtuale, campioni e culture it-IT/en-US...' -ForegroundColor Cyan
Invoke-CheckedDotnet -Stage 'Tests.ProcessLens.MemoryTests' -Project '.\tests\ProcessLens.MemoryTests\ProcessLens.MemoryTests.csproj' -Arguments @('run', '--project', '.\tests\ProcessLens.MemoryTests\ProcessLens.MemoryTests.csproj', '-c', 'Release')
Write-Host 'Test interpretazione approfondita dei processi...' -ForegroundColor Cyan
Invoke-CheckedDotnet -Stage 'Tests.ProcessLens.BehaviorTests' -Project '.\tests\ProcessLens.BehaviorTests\ProcessLens.BehaviorTests.csproj' -Arguments @('run', '--project', '.\tests\ProcessLens.BehaviorTests\ProcessLens.BehaviorTests.csproj', '-c', 'Release')
Write-Host 'Test terminologia e confini tra regole e osservazioni...' -ForegroundColor Cyan
Invoke-CheckedDotnet -Stage 'Tests.ProcessLens.TerminologyTests' -Project '.\tests\ProcessLens.TerminologyTests\ProcessLens.TerminologyTests.csproj' -Arguments @('run', '--project', '.\tests\ProcessLens.TerminologyTests\ProcessLens.TerminologyTests.csproj', '-c', 'Release')

Write-Host 'Test formattazione numerica, durate e rete (it-IT/en-US)...' -ForegroundColor Cyan
Invoke-CheckedDotnet -Stage 'Tests.ProcessLens.PresentationTests' -Project '.\tests\ProcessLens.PresentationTests\ProcessLens.PresentationTests.csproj' -Arguments @('run','--project','.\tests\ProcessLens.PresentationTests\ProcessLens.PresentationTests.csproj','-c','Release')

Write-Host 'Test copia testo, rapporti e tabelle...' -ForegroundColor Cyan
Write-Host 'Test nuova attivita: validazione e processo fixture posseduto...' -ForegroundColor Cyan
Invoke-CheckedDotnet -Stage 'Tests.ProcessLens.NewTaskTests' -Project '.\tests\ProcessLens.NewTaskTests\ProcessLens.NewTaskTests.csproj' -Arguments @('run','--project','.\tests\ProcessLens.NewTaskTests\ProcessLens.NewTaskTests.csproj','-c','Release')
Write-Host 'Test WPF nuova attivita: avvio simulato, nessun programma utente...' -ForegroundColor Cyan
Invoke-CheckedDotnet -Stage 'Tests.ProcessLens.NewTaskUiTests' -Project '.\tests\ProcessLens.NewTaskUiTests\ProcessLens.NewTaskUiTests.csproj' -Arguments @('run','--project','.\tests\ProcessLens.NewTaskUiTests\ProcessLens.NewTaskUiTests.csproj','-c','Release')
Invoke-CheckedDotnet -Stage 'Tests.ProcessLens.CopyTests' -Project '.\tests\ProcessLens.CopyTests\ProcessLens.CopyTests.csproj' -Arguments @('run','--project','.\tests\ProcessLens.CopyTests\ProcessLens.CopyTests.csproj','-c','Release')
Write-Host 'Test dispositivi rete: dati sintetici, riconoscimenti e layout nativo...' -ForegroundColor Cyan
Invoke-CheckedDotnet -Stage 'Tests.ProcessLens.NetworkDeviceTests' -Project '.\tests\ProcessLens.NetworkDeviceTests\ProcessLens.NetworkDeviceTests.csproj' -Arguments @('run','--project','.\tests\ProcessLens.NetworkDeviceTests\ProcessLens.NetworkDeviceTests.csproj','-c','Release')
Write-Host 'Test integrazione Wireshark: parser sintetici e processi fixture posseduti, nessuna cattura reale...' -ForegroundColor Cyan
Invoke-CheckedDotnet -Stage 'Tests.ProcessLens.PacketCaptureTests' -Project '.\tests\ProcessLens.PacketCaptureTests\ProcessLens.PacketCaptureTests.csproj' -Arguments @('run','--project','.\tests\ProcessLens.PacketCaptureTests\ProcessLens.PacketCaptureTests.csproj','-c','Release')
Write-Host 'Test rete nativa: eventi sintetici, policy e handle posseduti...' -ForegroundColor Cyan
Invoke-CheckedDotnet -Stage 'Tests.ProcessLens.NativeNetworkTests' -Project '.\tests\ProcessLens.NativeNetworkTests\ProcessLens.NativeNetworkTests.csproj' -Arguments @('run','--project','.\tests\ProcessLens.NativeNetworkTests\ProcessLens.NativeNetworkTests.csproj','-c','Release')
Write-Host 'Test cattura Windows e Wi-Fi: fixture, senza traffico reale...' -ForegroundColor Cyan
Invoke-CheckedDotnet -Stage 'Tests.ProcessLens.NativeCaptureTests' -Project '.\tests\ProcessLens.NativeCaptureTests\ProcessLens.NativeCaptureTests.csproj' -Arguments @('run','--project','.\tests\ProcessLens.NativeCaptureTests\ProcessLens.NativeCaptureTests.csproj','-c','Release')
Invoke-CheckedDotnet -Stage 'Tests.ProcessLens.CompatibleCaptureTests' -Project '.\tests\ProcessLens.CompatibleCaptureTests\ProcessLens.CompatibleCaptureTests.csproj' -Arguments @('run','--project','.\tests\ProcessLens.CompatibleCaptureTests\ProcessLens.CompatibleCaptureTests.csproj','-c','Release')
Write-Host 'Test WPF rete: sorgente simulata, nessuna lettura di rete reale...' -ForegroundColor Cyan
Invoke-CheckedDotnet -Stage 'Tests.ProcessLens.NetworkUiTests' -Project '.\tests\ProcessLens.NetworkUiTests\ProcessLens.NetworkUiTests.csproj' -Arguments @('run','--project','.\tests\ProcessLens.NetworkUiTests\ProcessLens.NetworkUiTests.csproj','-c','Release')

Write-Host 'Test inventario sistema: ABI, fixture e lettura Windows in sola lettura...' -ForegroundColor Cyan
Invoke-CheckedDotnet -Stage 'Tests.ProcessLens.SystemProcessTests' -Project '.\tests\ProcessLens.SystemProcessTests\ProcessLens.SystemProcessTests.csproj' -Arguments @('run','--project','.\tests\ProcessLens.SystemProcessTests\ProcessLens.SystemProcessTests.csproj','-c','Release')
Write-Host 'Test catalogo componenti Windows: filtri, gruppi e memoria...' -ForegroundColor Cyan
Invoke-CheckedDotnet -Stage 'Tests.ProcessLens.WindowsComponentTests' -Project '.\tests\ProcessLens.WindowsComponentTests\ProcessLens.WindowsComponentTests.csproj' -Arguments @('run','--project','.\tests\ProcessLens.WindowsComponentTests\ProcessLens.WindowsComponentTests.csproj','-c','Release')
Write-Host 'Test WPF sistema: sorgente simulata, selezione e annullamento...' -ForegroundColor Cyan
Invoke-CheckedDotnet -Stage 'Tests.ProcessLens.SystemProcessUiTests' -Project '.\tests\ProcessLens.SystemProcessUiTests\ProcessLens.SystemProcessUiTests.csproj' -Arguments @('run','--project','.\tests\ProcessLens.SystemProcessUiTests\ProcessLens.SystemProcessUiTests.csproj','-c','Release')

Write-Host 'Test WPF copia e caricamento markup, senza avvio monitor o clipboard reale...' -ForegroundColor Cyan
Invoke-CheckedDotnet -Stage 'Tests.ProcessLens.CopyUiTests' -Project '.\tests\ProcessLens.CopyUiTests\ProcessLens.CopyUiTests.csproj' -Arguments @('run','--project','.\tests\ProcessLens.CopyUiTests\ProcessLens.CopyUiTests.csproj','-c','Release')

Write-Host 'Test monitor RAM separato: totali, copertura e identita...' -ForegroundColor Cyan
Invoke-CheckedDotnet -Stage 'Tests.ProcessLens.RamMonitorTests' -Project '.\tests\ProcessLens.RamMonitorTests\ProcessLens.RamMonitorTests.csproj' -Arguments @('run','--project','.\tests\ProcessLens.RamMonitorTests\ProcessLens.RamMonitorTests.csproj','-c','Release')
Write-Host 'Test cache limitate e allocazioni dello storico (dati sintetici)...' -ForegroundColor Cyan
Invoke-CheckedDotnet -Stage 'Tests.ProcessLens.MemoryEfficiencyTests' -Project '.\tests\ProcessLens.MemoryEfficiencyTests\ProcessLens.MemoryEfficiencyTests.csproj' -Arguments @('run','--project','.\tests\ProcessLens.MemoryEfficiencyTests\ProcessLens.MemoryEfficiencyTests.csproj','-c','Release')

Write-Host 'Test policy scansione leggera e adattamento CPU/RAM...' -ForegroundColor Cyan
Invoke-CheckedDotnet -Stage 'Tests.ProcessLens.RefreshPolicyTests' -Project '.\tests\ProcessLens.RefreshPolicyTests\ProcessLens.RefreshPolicyTests.csproj' -Arguments @('run', '--project', '.\tests\ProcessLens.RefreshPolicyTests\ProcessLens.RefreshPolicyTests.csproj', '-c', 'Release')

Write-Host '[FASE 3] Test profiler .NET: parser gcdump storico/10, sicurezza e limiti...' -ForegroundColor Cyan
Invoke-CheckedDotnet -Stage 'Tests.ProcessLens.ProfilerTests' -Project '.\tests\ProcessLens.ProfilerTests\ProcessLens.ProfilerTests.csproj' -Arguments @('run', '--project', '.\tests\ProcessLens.ProfilerTests\ProcessLens.ProfilerTests.csproj', '-c', 'Release')
Write-Host 'Compilazione processo .NET sintetico (senza avviarlo)...' -ForegroundColor Cyan
Invoke-CheckedDotnet -Stage 'Build.ProcessLens.ProfilerFixture' -Project '.\tests\ProcessLens.ProfilerFixture\ProcessLens.ProfilerFixture.csproj' -Arguments @('build', '.\tests\ProcessLens.ProfilerFixture\ProcessLens.ProfilerFixture.csproj', '-c', 'Release', '--nologo')
Write-Host '[FASE 4] Compilazione profiler separato: nessun collegamento a Guardian...' -ForegroundColor Cyan
Invoke-CheckedDotnet -Stage 'Build.ProcessLens.Profiler' -Project '.\src\ProcessLens.Profiler\ProcessLens.Profiler.csproj' -Arguments @('build', '.\src\ProcessLens.Profiler\ProcessLens.Profiler.csproj', '-c', 'Release', '--nologo')
Write-Host '[FASE 5] Compilazione GUI WPF con controllo del markup XAML...' -ForegroundColor Cyan
Invoke-CheckedDotnet -Stage 'Build.ProcessLens.App' -Project '.\src\ProcessLens.App\ProcessLens.App.csproj' -Arguments @('build', '.\src\ProcessLens.App\ProcessLens.App.csproj', '-c', 'Release', '--nologo')
Write-Host '[FASE 6] Pubblicazione release GUI e profiler Windows x64...' -ForegroundColor Cyan
& (Join-Path $PSScriptRoot 'publish-release.ps1')
# publish-release.ps1 now throws if an underlying dotnet command fails.
if (-not (Test-Path '.\publish\win-x64\ProcessLens.exe')) {
    throw 'EXE ProcessLens non trovato nella cartella publish.'
}
Write-Host 'Pubblicazione agente avvisi RAM (sessione utente, avvio su consenso)...' -ForegroundColor Cyan
Invoke-CheckedDotnet -Stage 'Publish.ProcessLens.MemoryAgent' -Project '.\src\ProcessLens.MemoryAgent\ProcessLens.MemoryAgent.csproj' -Arguments @('publish', '.\src\ProcessLens.MemoryAgent\ProcessLens.MemoryAgent.csproj', '-c', 'Release', '-r', 'win-x64', '--self-contained', 'false', '-o', '.\publish\MemoryAgent-win-x64', '--nologo')
if (-not (Test-Path '.\publish\MemoryAgent-win-x64\ProcessLens.MemoryAgent.exe')) {
    throw 'MemoryAgent.exe non trovato dopo la pubblicazione.'
}
Write-Host 'Avvisi RAM non attivati automaticamente. Per attivarli: .\install-memory-alerts.ps1 da PowerShell NON amministrativo.' -ForegroundColor Yellow
# Separate read-only monitor, not started during build.
& (Join-Path $PSScriptRoot 'build-ram-monitor.ps1')
Write-BuildDiagnostic -EventId 'PL-BUILD-5003' -Stage 'BuildDashboard' -Result 'Completed' -Severity 'Information'
Write-Host '[OK] Compilazione e pubblicazione riuscite: .\run-release.ps1 oppure publish\win-x64\ProcessLens.exe' -ForegroundColor Green
Write-Host '[SICUREZZA] Guardian Windows Service e database esistente NON modificati.' -ForegroundColor Yellow
