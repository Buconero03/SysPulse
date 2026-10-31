"""Conservative source guards. No C#/.NET/WPF execution or numerical qualification."""
def violations(files):
    bad=[]
    def need(ok, label):
        if not ok: bad.append(label)
    ui=files['src/ProcessLens.App/MainWindow.xaml.cs']
    fast=ui[ui.index('    private async Task RefreshCpuAsync()'):ui.index('    private void CopySelfResourcesButton_Click')]
    tray=files['src/ProcessLens.App/TrayWindowController.cs']
    closing=tray[tray.index('    private void Closing('):tray.index('    private void Closed(')]
    policy=files['src/ProcessLens.App/WindowLifetimePolicy.cs']
    native=files['src/ProcessLens.Windows/WindowsLiveCpuSampler.cs']
    tracker=files['src/ProcessLens.Core/LiveCpuSample.cs']
    cache=files['src/ProcessLens.Core/BoundedMetadataCache.cs']
    self_memory=files['src/ProcessLens.Windows/WindowsSelfResources.cs']
    measure=files['measure-processlens-resources.ps1']
    need('RequestMinimize();' in closing and 'e.Cancel = true;' in closing and '_actions.Minimize();' in closing and '_window.ShowInTaskbar = true;' in closing and '_window.WindowState = WindowState.Minimized;' in closing and
         '_window.Hide()' not in closing and 'exitRequested ? WindowCloseAction.Exit : WindowCloseAction.MinimizeToTaskbar' in policy,
         'X must preserve taskbar route independently of tray')
    need('Mostra ProcessLens' in tray and 'Shell_NotifyIconW(1, ref existing)' in tray and
         'TaskbarCreated' in tray and '_window.Show();' in tray and '_window.WindowState = restoreState;' in tray,
         'explicit restore and lost tray entry recovery')
    need('Interval = MonitorRefreshPolicy.LiveMetricsInterval' in ui and 'LiveMetricsInterval = TimeSpan.FromSeconds(1)' in files['src/ProcessLens.Core/MonitorRefreshPolicy.cs'] and '_cpuTimer.Tick +=' in ui and
         not any(t in fast for t in ['_collector.Capture(', 'BuildRow(', 'ApplyFilter(', 'ShowDetails(', '_deepInspector.Inspect(']),
         'one-second CPU/RAM tick must not repeat expensive metadata or detail rebuilding')
    need('_cpuRefreshing || _windowClosed || !IsVisible || WindowState == WindowState.Minimized' in fast and
         'generation != _cpuGeneration' in fast and '_cpuTimer.Stop();' in ui and '_liveCpu.Reset();' in fast,
         'coalescing pause generation and baseline reset')
    need('Stopwatch.GetTimestamp()' in native and 'GetActiveProcessorCount(0xffff)' in native and
         'PROCESS_QUERY_LIMITED_INFORMATION' in native and 'finally { if (handle != IntPtr.Zero) NativeMethods.CloseHandle(handle); }' in native,
         'monotonic per-PID reads, machine logical count and limited released handles')
    need('_processes.Remove(pid); // A failed read breaks continuity' in native and
         'DateTime.FromFileTimeUtc' in native and 'cpus is > 0 and <= 64' in native,
         'failed continuity and processor-group bounds')
    need(all(t in tracker for t in ['old.Value.Start == startUtc', 'old.Value.Cpus == cpus',
         'totalTicks >= old.Value.Ticks', 'elapsed > 0 && elapsed <= MaximumGapSeconds',
         'MaximumGapSeconds = 6;', '_history.Count > 64', 'Math.Min(x.Seconds, x.End - (now - 30))',
         'p.StartTime.HasValue', 'ToUniversalTime()']), 'identity gaps normalization bounded window and unknown identity')
    need('PropertyChanged?.Invoke(this, change)' in files['src/ProcessLens.App/ProcessRow.cs'] and
         'row.UpdateCpu(' in fast and '? cpu.Apply(p) : LiveCpuSample.Unavailable(p)' in ui,
         'update existing row bindings and prevent old metadata CPU overwrites')
    need('_entries.Count == _capacity' in cache and '_entries.Remove(' in cache and 'StringComparer.OrdinalIgnoreCase' in cache and
         'BoundedMetadataCache<FileMetadata> _fileMetadataCache = new(512)' in files['src/ProcessLens.Windows/WindowsProcessCollector.cs'],
         'bounded case-insensitive metadata cache')
    need('PrivateWorkingSet = nuint.MaxValue' in self_memory and 'counters.PrivateWorkingSet <= counters.WorkingSet' in self_memory and
         'SelfResourcesText' in files['src/ProcessLens.App/MainWindow.xaml'] and 'Solo processo GUI' in fast,
         'self RAM distinct metrics and EX2 prefix detection')
    need('OpenProcess(0x1000' in measure and 'CloseHandle($handle)' in measure and
         'GetProcessTimes(' in measure and '$started.ToFileTimeUtc()' in measure and
         all(x in measure for x in ['CpuPercentTime','PrivateWorkingSetMiB','WorkingSetMiB','PrivateCommitMiB','AvailableSamples','SampledPeak']),
         'passive reproducible CPU/RAM report with process identity')
    need(not any(t in native+self_memory+measure for t in ['GC.Collect(', 'EmptyWorkingSet(', 'SetProcessWorkingSetSize(', 'TerminateProcess(', 'Start-Process ', 'Stop-Process ']),
         'no fake working-set savings or process actions')
    need('Tests.ProcessLens.CpuTests' in files['build-dashboard.ps1'], 'CPU regressions in normal build gate')
    return bad
