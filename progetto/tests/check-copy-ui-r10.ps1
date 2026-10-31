$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$code = Get-Content (Join-Path $root 'src\ProcessLens.App\MainWindow.xaml.cs') -Raw -Encoding UTF8
foreach ($file in @('MainWindow.xaml','GuardianDashboardView.xaml','MemoryDashboardView.xaml')) {
    [xml]$view = Get-Content (Join-Path $root ('src\ProcessLens.App\' + $file)) -Raw -Encoding UTF8
    $fields = @($view.SelectNodes("//*[local-name()='SelectableText']"))
    if ($fields.Count -eq 0) { throw ('FAIL copy: no selectable output in ' + $file) }
    foreach ($node in $fields) {
        if ($node.HasAttribute('LineHeight') -or $node.HasAttribute('TextTrimming')) { throw 'FAIL copy: unsupported TextBox attribute.' }
        $binding = $node.GetAttribute('Text')
        if ($binding.StartsWith('{Binding') -and -not $binding.Contains('Mode=OneWay')) { throw 'FAIL copy: output binding must be OneWay.' }
    }
    foreach ($grid in $view.SelectNodes("//*[local-name()='DataGrid']")) {
        if ($grid.GetAttribute('GridCopySupport.Enabled','clr-namespace:ProcessLens.App') -ne 'True') { throw 'FAIL copy: table missing clipboard support.' }
    }
}
$control = Get-Content (Join-Path $root 'src\ProcessLens.App\SelectableText.cs') -Raw -Encoding UTF8
if (-not $control.Contains('IsReadOnlyProperty.OverrideMetadata') -or -not $control.Contains('ApplicationCommands.Copy') -or
    -not $control.Contains('CopyAllCommand')) { throw 'FAIL copy: read-only and copy commands missing.' }
foreach ($token in @('_copyAnswerBody = result.Answer;', 'CopyTextFormat.Report', 'CopyQueryReport_Click',
    '_copyAnswerBody = string.Empty;', 'new SelectableText', 'MemoryDashboard.InspectProcessRequested +=')) {
    if (-not $code.Contains($token)) { throw ('FAIL copy: missing integration: ' + $token) }
}
if ($code.Contains('new TextBlock') -or $code.Contains('LineHeight =')) { throw 'FAIL copy: generated report still uses non-selectable or incompatible output.' }
foreach ($file in @('publish-release.ps1','run-release.ps1','start-processlens-all.ps1')) {
    $script = Get-Content (Join-Path $root $file) -Raw -Encoding UTF8
    if ($script.Contains("Get-Process -Name 'ProcessLens' -ErrorAction SilentlyContinue | Stop-Process")) {
        throw ('FAIL copy/review: automatic GUI termination remains in ' + $file)
    }
}
Write-Host 'PASS R10 copy: selectable output, one-way bindings, grid copy, complete report and no automatic GUI termination. Structural checks only.'
$layout = Get-Content (Join-Path $root 'src\ProcessLens.App\ReportLayout.cs') -Raw -Encoding UTF8
$reportView = Get-Content (Join-Path $root 'src\ProcessLens.App\ReportView.cs') -Raw -Encoding UTF8
foreach ($token in @('ProcessQueryOverviewLabel', 'DetailEvidenceOverviewText', 'ReportLayout.Sections(result.Answer)',
    'ReportView.Body', '_renderedEvidenceGroupsPid', 'expanded.Contains(groupName)')) {
    if (-not $code.Contains($token)) { throw ('FAIL report hierarchy: missing integration: ' + $token) }
}
foreach ($token in @('TextWrapping = TextWrapping.Wrap', 'header.MaxWidth', 'item.Content = content()', 'item.Content = null')) {
    if (-not $reportView.Contains($token)) { throw ('FAIL report hierarchy: missing native layout/lifecycle: ' + $token) }
}
if (-not $layout.Contains('else lines.Add(line);')) { throw 'FAIL report hierarchy: unheaded source lines are not retained.' }
Write-Host 'PASS R10 R3 hierarchy: overview, chapters, wrapping, source retention and lazy controls. Source checks only.'
$watch = Get-Content (Join-Path $root 'src\ProcessLens.App\ProcessExitWatch.cs') -Raw -Encoding UTF8
foreach ($token in @('TrackSelectedProcessExit(row)', 'ProcessExitObserved', '_processNoticeTimer.Stop();',
    '_selectedExitWatch?.Dispose();', '_requestedExitWatch?.Dispose();', 'ProcessNoticeCard')) {
    if (-not $code.Contains($token)) { throw ('FAIL exit notice: missing UI/lifecycle: ' + $token) }
}
foreach ($token in @('Synchronize | QueryLimitedInformation', 'GetProcessTimes(handle.SafeWaitHandle',
    'DateTime.FromFileTimeUtc(created).Ticks != identity.StartUtcTicks', 'executeOnlyOnce: true')) {
    if (-not $watch.Contains($token)) { throw ('FAIL exit notice: missing verified read-only signal: ' + $token) }
}
Write-Host 'PASS R10 R4 exit notice: verified instance, Windows exit signal and bounded UI lifecycle. Source checks only.'
$tray = Get-Content (Join-Path $root 'src\ProcessLens.App\TrayWindowController.cs') -Raw -Encoding UTF8
$app = Get-Content (Join-Path $root 'src\ProcessLens.App\App.xaml.cs') -Raw -Encoding UTF8
foreach ($token in @('WindowLifetimePolicy.Close(', 'e.Cancel = true;', '_window.ShowInTaskbar = true;',
    'Mostra ProcessLens', 'Esci da ProcessLens', '_window.Show();', 'TaskbarCreated', 'SendMessageTimeoutW')) {
    if (-not $tray.Contains($token)) { throw ('FAIL tray lifecycle: missing restore/exit route: ' + $token) }
}
if (-not $app.Contains('ShutdownMode = ShutdownMode.OnExplicitShutdown;') -or
    -not $app.Contains('new TrayWindowController(window') -or -not $app.Contains('TrayWindowController.TryRestoreExisting()')) {
    throw 'FAIL tray lifecycle: startup or existing-instance restore not wired.'
}
Write-Host 'PASS R10 R7 lifecycle: taskbar restore route, Mostra, explicit exit and shell recovery wired. Source checks only.'
$audit = Get-Content (Join-Path $root 'src\ProcessLens.App\AuditLogView.cs') -Raw -Encoding UTF8
foreach ($token in @('ProcessQueryAuditText_TextChanged', 'AuditLogPresentation.Parse(ProcessQueryAuditText.Text)',
    'ProcessQueryAuditPanel_Expanded', 'ProcessQueryAuditPanel_Collapsed', 'CopyQueryAudit_Click')) {
    if (-not $code.Contains($token)) { throw ('FAIL audit layout: missing actual audit integration: ' + $token) }
}
foreach ($token in @('FontSize = 12.5', 'Padding = new Thickness(10)', 'Margin = new Thickness(0, 0, 0, 6)',
    '_rendered.SequenceEqual(document.Entries.Take(_rendered.Count))')) {
    if (-not $audit.Contains($token)) { throw ('FAIL audit layout: missing spacing or append lifecycle: ' + $token) }
}
Write-Host 'PASS R10 R6 audit layout: timestamp/stage/message cards, spacing, append and original copy wired. Source checks only.'

foreach ($token in @('Interval = MonitorRefreshPolicy.LiveMetricsInterval', 'generation != _cpuGeneration',
    'RecentCpu(int pid)', 'row.UpdateCpu(', 'CopySelfResourcesButton_Click')) {
    if (-not $code.Contains($token)) { throw ('FAIL R10 R7 resources: ' + $token) }
}
Write-Host 'PASS R10 R7 resources: CPU/RAM tick, stale generation, sample age and self-resource copy wired. Source checks only.'
