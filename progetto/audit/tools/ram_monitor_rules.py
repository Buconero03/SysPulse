"""R8 source-only invariants, not C# compilation or runtime proof."""
def violations(files):
    bad=[]
    def need(ok,name):
        if not ok:bad.append(name)
    model=files['src/ProcessLens.RamMonitor/Measurement.cs'];native=files['src/ProcessLens.RamMonitor/NativeSampler.cs']
    logs=files['src/ProcessLens.RamMonitor/LogSession.cs'];program=files['src/ProcessLens.RamMonitor/Program.cs']
    ui=files['src/ProcessLens.App/MainWindow.xaml.cs'];memory=files['src/ProcessLens.App/MemoryDashboardView.xaml.cs']
    nav=files['src/ProcessLens.App/ServiceNavigation.cs'];view=files['src/ProcessLens.App/MemoryDashboardView.xaml']
    need('complete ? observed : null' in model and 'enumerationComplete && unresolved == 0 && values.Length == members.Length' in model,
         'missing values or enumeration cannot become complete totals')
    need('child.HandleParentPid == parent.Pid' in model and 'child.StartUtcTicks.Value >= parent.StartUtcTicks.Value' in model and
         'NativeLibrary.TryGetExport' in native and 'NtQueryInformationProcess' in native,'same-handle parent and creation check')
    need('_scope.Contains(reading.Path)' in native and 'previous.StartUtcTicks == reading.StartUtcTicks' in native,
         'roots need exact path and retained children need exact creation')
    need('Environment.ProcessId' in native and 'MonitorExcluded' in native and 'capture.Processes' in logs and 'capture.Monitor' in logs,
         'meter is tracked separately from suite aggregate')
    need('OpenProcess(0x1000' in native and 'finally { CloseHandle(handle); }' in native and 'finally { CloseHandle(snapshot); }' in native,
         'limited rights and released handles')
    need('PrivateWorkingSet = UIntPtr.MaxValue' in native and 'memory && ex2' in native and 'Marshal.OffsetOf<Counters>' in native,
         'EX prefix fallback never invents private resident bytes')
    need(not any(x in native+program for x in ['TerminateProcess(', 'EmptyWorkingSet(', 'SetProcessWorkingSetSize(', 'GC.Collect(', 'Process.Start(']),
         'meter does not change or start monitored processes')
    need(all(x in logs for x in ['processes.csv','totals.csv','summary.json','scope.json','CultureInfo.InvariantCulture','\n        Flush(); SaveSummary','LimitReached']),
         'raw log, totals, scope, summary, invariant numeric CSV, flush and size limit')
    need('ReadFullAnalysis(int.MaxValue)' in memory and 'DetailsRequested?.Invoke' in memory and
         'new(row.TargetPid, row.ExecutableName, row.Grouped, _lastSnapshotStartedUtc)' in memory,
         'full RAM category list and snapshot-bound detail request')
    need(all('Category'+n+'Card' in view for n in ['Apps','Dev','Win','Services','Residual']) and 'MemoryCategoryCard' in view and
         'ResidualCategoryDetails' in view, 'accessible category buttons and residual limits')
    need('host.SnapshotStartUtc == host.CurrentStartUtc' in nav and 'host.Pid == target.Pid' in nav and
         'scope.ContainsKey(pid)' in nav and 'host.CurrentStartUtc!.Value <= target.FrameStartedUtc.Value' in nav,
         'navigation uses exact PID and known instance existing when RAM acquisition began')
    need('request != _workspaceNavigation' in ui and 'navigation != _workspaceNavigation' in ui and
         'ReadProcessStartUtc(p.Pid) == p.SnapshotStartUtc' in ui and 'ServiceNavigation.Includes(service.ProcessId, _serviceHostScope)' in ui,
         'async service navigation and later catalog refreshes cannot publish another scope')
    details=files['src/ProcessLens.App/ServiceDetailsWindow.cs']
    need('ReportView.Section' in details and 'new SelectableText' in details and
         not any(t in details for t in ['StopService(', 'TerminateProcess(', 'Process.Start(', 'ServiceStopPolicy']),
         'service popup is read-only and selectable')
    return bad
