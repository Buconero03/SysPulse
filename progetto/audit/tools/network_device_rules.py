"""Source guards, not .NET execution or proof of network coverage."""
def violations(files):
    native = files['src/ProcessLens.Windows/WindowsNetworkDevices.cs']
    core = files['src/ProcessLens.Core/NetworkDevices.cs']
    view = files['src/ProcessLens.App/NetworkDevicesView.xaml.cs']
    main = files['src/ProcessLens.App/MainWindow.xaml.cs']
    errors = []
    def need(ok, label):
        if not ok: errors.append(label)
    need('GetIpNetTable2(0, out table)' in native and
         'finally { if (table != IntPtr.Zero) FreeMibTable(table); }' in native,
         'IPv4 plus IPv6 local table and released native memory')
    need('Marshal.OffsetOf<NativeHeader>(nameof(NativeHeader.First))' in native and
         'Marshal.SizeOf<NativeRow>()' in native and 'Size = 28' in native,
         'native row and aligned table sizes derived through marshaling')
    need('MaximumRows = 4096' in native and 'count > MaximumRows' in native and
         'Math.Min(count, (uint)MaximumRows)' in native, 'bounded native enumeration with explicit truncation')
    need('row.Flags & 2' in native and 'NeighborState.Unreachable' in native and
         'State = (uint)' not in native and 'truncated = true; continue;' in native,
         'unreachable flag and unsupported rows cannot turn into confirmed presence')
    need('adapter.Local.Any(' in native and 'adapter.Broadcast.Any(' in native and
         'IsUnicastAddress(n.Address.ToString())' in native, 'local and multicast or directed broadcast rows excluded')
    need('bool changed = !adapters.Select' in native and 'changed ? ""' in native and
         'partial |= afterPartial || truncated || changed' in native, 'configuration change invalidates the scope and complete status')
    need(not any(x in native + view for x in ['Process.Start(', 'DispatcherTimer', 'new Ping(', 'Dns.Get',
         'ResolveIpNetEntry2(', 'SetIpNetEntry2(', 'FlushIpNetTable2(', 'Socket.Connect']), 'no shell timers probes lookups or network changes')
    refresh = view[view.index('internal async Task RefreshAsync()'):view.index('private void Render()')]
    inactive = view[view.index('public void SetActive(bool active)'):view.index('private async void RefreshNetwork_Click')]
    need('if (!_active || _busy) return;' in refresh and 'Task.Run(' in refresh and
         'generation != _generation' in refresh, 'manual worker coalescing and stale-result publication guard')
    need('_source.Capture()' not in inactive and '_store.Load()' not in inactive and
         'NetworkDevicesTable.ItemsSource = null;' in inactive and '_snapshot = null; _view = null;' in inactive,
         'activation is passive and deactivation releases displayed data')
    need('_snapshot.Available && !_snapshot.Partial' in view and
         'if (!_active || _busy || !_current || !_catalog.Available' in view and 'CanRecognize: true' in view,
         'recognition requires a complete current capture and valid selected identity')
    need('capture.Partial ||' in core and '!catalog.Available' in core and 'devices == 0' in core and
         'limited ? "#64748B"' in core and '"Dispositivi: N/D"' in core,
         'empty partial failed or unrecognized scope cannot become a green security claim')
    need('In cache · presenza non confermata' in core and 'Voce statica · presenza non confermata' in core,
         'cached and permanent neighbors are not asserted connected now')
    need('n.InterfaceId + "/" + n.NetworkKey' in core and 'RecognitionKey(n.NetworkKey, mac)' in core and
         'NormalizeMac(g.Mac).Length == 0' in core and 'SHA256.HashData' in core,
         'grouping includes adapter and recognition includes a gateway-derived network scope')
    need('if (_seen.Count == 16)' in core and 'current.Count > 1024' in core and
         'if (!snapshot.Available || snapshot.Partial' in core and '_seen[group.Key] = current;\n                continue;' in core,
         'bounded per-network baseline and no alerts from an initial or partial observation')
    need('MaximumDevices = 512' in core and 'MaximumBytes = 524288' in core and
         'stream.Length > MaximumBytes' in core and 'if (!existing.Available) return existing.Error;' in core,
         'bounded registry and corrupt or unreadable catalog cannot be replaced silently')
    need('ex is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or ArgumentException' in core,
         'semantic catalog validation errors become unavailable data rather than unhandled exceptions')
    need('File.Move(temporary, _path, true)' in core and 'File.Delete(temporary)' in core and
         'd.Name.Length is > 0 and <= 80' in core, 'atomic persistence and bounded user labels')
    need('network && NetworkDevices is null' in main and 'NetworkDevicesHost.Content = NetworkDevices;' in main and
         'NetworkDevices?.SetActive(active && _networkMode)' in main and
         '!_guardianMode && !_memoryMode && !_networkMode' in main,
         'lazy navigation and window lifecycle gates with no metadata polling for network workspace')
    need('Tests.ProcessLens.NetworkDeviceTests' in files['build-dashboard.ps1'] and
         'Tests.ProcessLens.NetworkUiTests' in files['build-dashboard.ps1'] and
         'check-network-devices-r12.ps1' in files['build-dashboard.ps1'], 'independent executable tests and source gate in normal Windows build')
    return errors
