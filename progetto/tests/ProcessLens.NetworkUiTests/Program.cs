using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Threading;
using ProcessLens.App;
using ProcessLens.Core;

internal static class Program
{
    private static void Check(bool ok, string label)
    {
        if (!ok) throw new Exception("FAIL network UI: " + label);
        Console.WriteLine("PASS network UI: " + label);
    }
    private static void Complete(Task task)
    {
        var dispatcher = Dispatcher.CurrentDispatcher;
        var frame = new DispatcherFrame();
        bool timedOut = false;
        var timeout = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
        timeout.Tick += (_, _) => { timedOut = true; frame.Continue = false; };
        task.ContinueWith(_ => dispatcher.BeginInvoke(new Action(() => frame.Continue = false)), TaskScheduler.Default);
        timeout.Start(); Dispatcher.PushFrame(frame); timeout.Stop();
        if (timedOut) throw new Exception("Network UI fixture timed out.");
        task.GetAwaiter().GetResult();
    }
    private static async Task VerifyDispatcherContinuationAsync(Task<int> completion, Dispatcher dispatcher, int ownerThread)
    {
        int workerThread = await completion;
        Check(workerThread != ownerThread, "R14 R3 fixture completion comes from a worker thread");
        Check(dispatcher.CheckAccess() && Environment.CurrentManagedThreadId == ownerThread &&
            SynchronizationContext.Current is DispatcherSynchronizationContext,
            "R14 R3 awaited continuation returns to the owning WPF dispatcher");
    }
    [STAThread]
    public static void Main()
    {
        _ = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var previousContext = SynchronizationContext.Current;
        var dispatcher = Dispatcher.CurrentDispatcher;
        int ownerThread = Environment.CurrentManagedThreadId;
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
        string root = Path.Combine(Path.GetTempPath(), "ProcessLens-NetworkUi-" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(root, "devices.json");
        using var source = new FixtureSource();
        try
        {
            Check(SynchronizationContext.Current is DispatcherSynchronizationContext,
                "R14 R3 WPF context installed before starting asynchronous operations");
            var completion = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            Task continuation = VerifyDispatcherContinuationAsync(completion.Task, dispatcher, ownerThread);
            // Queue only after the incomplete task has been awaited, guaranteeing an actual asynchronous continuation.
            ThreadPool.QueueUserWorkItem(_ => completion.SetResult(Environment.CurrentManagedThreadId));
            Complete(continuation);
            var view = new NetworkDevicesView(source, new DeviceRecognitionStore(path));
            Check(source.Calls == 0 && !File.Exists(path), "construction is lazy and does not read a real network or write settings");
            var cacheTabs = (TabControl)view.FindName("NetworkTabs");
            Check(cacheTabs.SelectedIndex == 0, "R14 R2 cache tab explicitly selected before Loaded or visual layout");
            view.SetActive(true);
            Check(source.Calls == 0, "activation does not automatically acquire network data");
            var grid = (DataGrid)view.FindName("NetworkDevicesTable");
            var indicator = (SelectableText)view.FindName("NetworkIndicatorText");
            var notice = (SelectableText)view.FindName("NetworkNoticeText");
            var recognize = (Button)view.FindName("RecognizeNetworkButton");
            Check(GridCopySupport.GetEnabled(grid) && grid.IsReadOnly && grid.EnableRowVirtualization,
                "real table is read-only virtualized and supports copy");
            Check(((Button)view.FindName("RefreshNetworkButton")).IsEnabled,
                "R14 R2 manual refresh enabled before the control is shown");
            Complete(view.RefreshAsync());
            Check(source.Calls == 1, $"first explicit refresh calls fixture once; calls={source.Calls}, tab={cacheTabs.SelectedIndex}");
            Check(grid.Items.Count == 1, $"first explicit refresh binds one row; rows={grid.Items.Count}, status={indicator.Text}, notice={notice.Text}");
            Check(notice.Text == "", $"first explicit refresh establishes baseline without a false new-device notice; notice={notice.Text}");
            Check(((Border)view.FindName("NetworkEmptyBorder")).Visibility == Visibility.Collapsed &&
                ((SelectableText)view.FindName("NetworkCountsText")).Text.Contains("Voci con MAC: 1") &&
                ((SelectableText)view.FindName("NetworkJournalText")).Text.Contains("ESITO"), "R34 successful device read has counts and a session log");
            grid.SelectedIndex = 0;
            ((TextBox)view.FindName("NetworkNameBox")).Text = "Telefono 🧪";
            Check(recognize.IsEnabled, "explicit recognition enabled for a scoped MAC");
            Complete(view.SaveSelectionAsync(false));
            Check(File.Exists(path) && grid.Items[0] is NetworkDeviceRow { Recognized: true, Name: "Telefono 🧪" },
                "actual recognition action saves and refreshes the selected row");
            Check(GridCopySupport.CaptureTable(grid, false).Contains("Telefono 🧪"), "table copy includes the chosen Unicode name without changing selection");
            var second = source.Snapshot.Neighbors[0] with { Mac = "02-11-22-33-44-66", Address = "192.168.1.3" };
            source.Snapshot = source.Snapshot with { Neighbors = source.Snapshot.Neighbors.Append(second).ToArray() };
            Complete(view.RefreshAsync());
            Check(notice.Text == "Nuova voce con MAC osservata rispetto alle letture di questa sessione.", "new unknown MAC produces the inline notice");
            grid.SelectedItem = grid.Items.Cast<NetworkDeviceRow>().Single(r => r.Recognized);
            Complete(view.SaveSelectionAsync(true));
            Check(new DeviceRecognitionStore(path).Load().Devices.Count == 0, "remove recognition persists an empty catalog");
            source.Block();
            int before = source.Calls;
            Task pending = view.RefreshAsync();
            Check(source.Started.Wait(TimeSpan.FromSeconds(5)), "blocked acquisition fixture starts on worker");
            Complete(view.RefreshAsync());
            Check(source.Calls == before + 1 && !recognize.IsEnabled, "concurrent refresh coalesces and recognition stays disabled while pending");
            view.SetActive(false); source.Release.Set(); Complete(pending);
            Check(notice.Text.Contains("Sezione chiusa") && !recognize.IsEnabled && grid.Items.Count == 0,
                "deactivation releases rows discards an in-flight result and disables actions");
            view.SetActive(true);
            source.Snapshot = source.Snapshot with { Available = false, Note = "errore sintetico" };
            Complete(view.RefreshAsync());
            Check(grid.Items.Count == 0 && indicator.Text == "Controllo non disponibile" &&
                ((SelectableText)view.FindName("NetworkDetailsText")).Text.Contains("errore sintetico"),
                "failed acquisition clears stale device rows and preserves error text");
            source.Snapshot = source.Snapshot with { Available = true, Partial = true };
            Complete(view.RefreshAsync()); grid.SelectedIndex = 0;
            Check(!recognize.IsEnabled && indicator.Text == "Dati incompleti", "partial capture cannot enable identity labeling or show green");
            view.SetActive(false);

            var packets = new PacketFixture();
            var captureView = new NetworkCaptureView(packets);
            Check(packets.Calls == 0, "packet view construction does not find tools or start child processes");
            captureView.SetActive(true);
            Check(packets.Calls == 0, "packet view activation does not acquire or probe automatically");
            Complete(captureView.DiscoverAsync());
            var adapters = (ComboBox)captureView.FindName("CaptureInterfacesBox");
            var startCapture = (Button)captureView.FindName("StartCaptureButton");
            Check(adapters.Items.Count == 1 && adapters.SelectedIndex == -1 && !startCapture.IsEnabled,
                "capture requires explicit interface selection and never defaults to the first device");
            adapters.SelectedIndex = 0;
            ((CheckBox)captureView.FindName("CaptureMonitorBox")).IsChecked = true;
            Check(!startCapture.IsEnabled, "monitor capture is blocked until a capability check succeeds");
            Complete(captureView.VerifyMonitorAsync());
            Check(startCapture.IsEnabled, "declared monitor link type unlocks the selected capture mode");
            Complete(captureView.CaptureAsync(Path.Combine(root, "test.pcapng")));
            Check(packets.LastMonitor, "actual capture UI passes the explicit monitor choice to its client");
            Complete(captureView.AnalyzeAsync(Path.Combine(root, "test.pcapng")));
            var packetGrid = (DataGrid)captureView.FindName("CaptureObservationsTable");
            Check(packetGrid.Items.Count == 1 && GridCopySupport.CaptureTable(packetGrid, false).Contains("02-11-22-33-44-55"),
                "actual packet grid binds observed fields and supports copying without the OS clipboard");
            packets.Block = new TaskCompletionSource<PacketEvidence>(TaskCreationOptions.RunContinuationsAsynchronously);
            Task late = captureView.AnalyzeAsync(Path.Combine(root, "test.pcapng"));
            captureView.SetActive(false); packets.Block.SetResult(PacketFixture.Evidence); Complete(late);
            Check(packetGrid.Items.Count == 0 && !startCapture.IsEnabled,
                "packet deactivation releases rows and rejects late results even if a client ignores cancellation");

            using var nativeCaptureFixture = new NativeCaptureFixture();
            var nativeCapture = new NativeCaptureView(nativeCaptureFixture);
            Check(nativeCaptureFixture.Calls == 0, "R17 native capture constructor performs no API call");
            nativeCapture.SetActive(true);
            Check(nativeCaptureFixture.Calls == 0, "R17 activation performs no discovery or capture");
            Complete(nativeCapture.DiscoverAsync());
            var nativeSources = (ComboBox)nativeCapture.FindName("NativeCaptureSourcesBox");
            var nativeStart = (Button)nativeCapture.FindName("StartNativeCaptureButton");
            Check(nativeSources.Items.Count == 1 && nativeSources.SelectedIndex == -1 && !nativeStart.IsEnabled,
                "R17 discovery does not implicitly choose a capture source");
            nativeSources.SelectedIndex = 0; Complete(nativeCapture.CaptureAsync());
            var nativePackets = (DataGrid)nativeCapture.FindName("NativePacketsTable");
            Check(nativeCaptureFixture.Captures == 1 && nativePackets.Items.Count == 1 && nativePackets.IsReadOnly && GridCopySupport.GetEnabled(nativePackets),
                "R17 explicit capture binds copyable virtualized endpoint metadata");
            nativePackets.SelectedIndex = 0;
            Check(((SelectableText)nativeCapture.FindName("NativeCaptureSelectionText")).Text.Contains("Byte campionati"), "R34 packet selection explains the observation");
            Complete(nativeCapture.ReadWifiAsync());
            var wifiGrid = (DataGrid)nativeCapture.FindName("WifiBssTable");
            Check(wifiGrid.Items.Count == 1 && nativePackets.Items.Count == 0 && GridCopySupport.CaptureTable(wifiGrid, false).Contains("Rete fixture"),
                "R17 Wi-Fi cache has a separate copyable table and releases previous capture rows");
            wifiGrid.SelectedIndex = 0;
            Check(((SelectableText)nativeCapture.FindName("NativeCaptureSelectionText")).Text.Contains("RSSI"), "R34 Wi-Fi selection explains radio cache");
            nativeCaptureFixture.Pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
            Task pendingWifi = nativeCapture.ReadWifiAsync();
            Check(nativeCaptureFixture.Started.Wait(TimeSpan.FromSeconds(5)), "R17 Wi-Fi fixture is pending on a worker");
            int calls = nativeCaptureFixture.Calls;
            Complete(nativeCapture.ReadWifiAsync());
            Check(nativeCaptureFixture.Calls == calls && !nativeStart.IsEnabled, "R17 pending native operation coalesces repeated requests");
            nativeCapture.SetActive(false); nativeCapture.SetActive(true);
            nativeCaptureFixture.Pending.SetResult(NativeCaptureFixture.Wifi); Complete(pendingWifi);
            Check(wifiGrid.Items.Count == 0 && nativeSources.Items.Count == 0 && nativeCaptureFixture.Released > 0,
                "R17 close and reopen rejects late Wi-Fi results and releases native discovery");
            nativeCaptureFixture.Pending = null;
            nativeCaptureFixture.WifiFailure = true; Complete(nativeCapture.ReadWifiAsync());
            Check(wifiGrid.Items.Count == 0 && ((SelectableText)nativeCapture.FindName("NativeCaptureStatusText")).Text.Contains("N/D") &&
                ((SelectableText)nativeCapture.FindName("NativeCaptureNoteText")).Text.Contains("access denied"),
                "R17 unavailable Wi-Fi retains the reason and never becomes zero observed clients");
            nativeCapture.SetActive(false); nativeCapture.SetActive(true);
            Check(((SelectableText)nativeCapture.FindName("NativeCaptureNoteText")).Text.Contains("fixture access denied") &&
                ((Button)nativeCapture.FindName("CopyNativeCaptureButton")).IsEnabled,
                "R18 error diagnostics survive tab leave and reopen and remain copyable");
            nativeCapture.SetActive(false);

            var window = new MainWindow();
            var host = (ContentControl)window.FindName("NetworkDevicesHost");
            Check(host.Content is null, "main window does not eagerly construct network view");
            ((Button)window.FindName("NavNetworkButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(host.Content is NetworkDevicesView && host.Visibility == Visibility.Visible &&
                ((Grid)window.FindName("ProcessWorkspace")).Visibility == Visibility.Collapsed,
                "Rete button opens its own lazy workspace");
            var actualNetwork = host.Content as NetworkDevicesView
                ?? throw new Exception("FAIL network UI: expected NetworkDevicesView in network host");
            var packetHost = (ContentControl)actualNetwork.FindName("NetworkCaptureHost");
            Check(packetHost.Content is null, "packet capture pane is lazy inside the network workspace");
            ((TabControl)actualNetwork.FindName("NetworkTabs")).SelectedIndex = 1;
            Check(packetHost.Content is NativeCaptureView &&
                ((ComboBox)((NativeCaptureView)packetHost.Content).FindName("NativeCaptureSourcesBox")).Items.Count == 0,
                "packet tab builds the real view without enumerating adapters or starting tools");
            ((Button)window.FindName("NavProcessesButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(host.Visibility == Visibility.Collapsed && ((Grid)window.FindName("ProcessWorkspace")).Visibility == Visibility.Visible,
                "Processi restores the existing workspace");
            var nativeHost = (ContentControl)actualNetwork.FindName("NativeNetworkHost");
            Check(nativeHost.Content is null, "R16 native evidence pane stays lazy while packet tab is used");
            ((Button)window.FindName("NavNetworkButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            ((TabControl)actualNetwork.FindName("NetworkTabs")).SelectedIndex = 2;
            Check(nativeHost.Content is NativeNetworkEvidenceView &&
                ((DataGrid)((NativeNetworkEvidenceView)nativeHost.Content).FindName("NativeNetworkEventsTable")).Items.Count == 0,
                "R16 native tab constructs without reading Security or audit policy");
            window.Close();

            using var tabSource = new FixtureSource();
            var tabView = new NetworkDevicesView(tabSource, new DeviceRecognitionStore(Path.Combine(root, "tabs.json")));
            var tabs = (TabControl)tabView.FindName("NetworkTabs");
            var captureHost = (ContentControl)tabView.FindName("NetworkCaptureHost");
            tabView.SetActive(true);
            Check(tabSource.Calls == 0 && captureHost.Content is null,
                "R14 R2 selecting the initial cache tab does not query data or eagerly create packet capture");
            tabs.SelectedIndex = 1;
            Complete(tabView.RefreshAsync());
            Check(tabSource.Calls == 0, "R14 R2 Wireshark tab cannot refresh the cache source");
            tabs.SelectedIndex = 0;
            Check(((SelectableText)tabView.FindName("NetworkUpdatedText")).Text == "Nessuna lettura corrente" &&
                ((Border)tabView.FindName("NetworkEmptyBorder")).Visibility == Visibility.Visible,
                "R34 invalidated cache cannot retain an old timestamp or unexplained empty grid");
            Check(tabSource.Calls == 0, "R14 R2 returning to cache tab does not automatically refresh");
            Complete(tabView.RefreshAsync());
            Check(tabSource.Calls == 1 && ((DataGrid)tabView.FindName("NetworkDevicesTable")).Items.Count == 1,
                "R14 R2 explicit cache refresh works after switching tabs before Loaded");
            tabView.SetActive(false);
            using var nativeSource = new NativeFixture();
            var nativeView = new NativeNetworkEvidenceView(nativeSource);
            Check(nativeSource.Calls == 0, "R16 native view constructor does not invoke Windows APIs");
            nativeView.SetActive(true);
            Check(nativeSource.Calls == 0, "R16 native activation does not automatically read logs");
            Complete(nativeView.RefreshAsync());
            var nativeGrid = (DataGrid)nativeView.FindName("NativeNetworkEventsTable");
            Check(nativeSource.Calls == 1 && nativeGrid.Items.Count == 1 && nativeGrid.IsReadOnly && GridCopySupport.GetEnabled(nativeGrid),
                "R16 manual native acquisition binds copied virtualized event data");
            Check(((SelectableText)nativeView.FindName("NativeEventCountsText")).Text.Contains("Connessioni bloccate: 1") &&
                ((Border)nativeView.FindName("NativeEventEmptyBorder")).Visibility == Visibility.Collapsed, "R34 events have per-family counts and clear empty states");
            nativeGrid.SelectedIndex = 0;
            Check(((SelectableText)nativeView.FindName("NativeNetworkSelectionText")).Text.Contains("PID può essere stato riutilizzato") &&
                GridCopySupport.CaptureTable(nativeGrid, false).Contains("192.0.2.8"),
                "R16 event selection and table copy retain reported historical source and PID limits");
            nativeSource.Started.Reset(); nativeSource.Release.Reset(); nativeSource.Block = true;
            Task nativePending = nativeView.RefreshAsync();
            Check(nativeSource.Started.Wait(TimeSpan.FromSeconds(5)), "R16 blocked native fixture starts on worker");
            Complete(nativeView.RefreshAsync());
            Check(nativeSource.Calls == 2, "R16 concurrent native refreshes coalesce");
            nativeView.SetActive(false); nativeSource.Release.Set(); Complete(nativePending);
            Check(nativeGrid.Items.Count == 0 && !((Button)nativeView.FindName("ReadNativeNetworkButton")).IsEnabled,
                "R16 hidden pane rejects late native results and releases event rows");
            nativeSource.Block = false;
            ((ComboBox)nativeView.FindName("NativeEventWindowBox")).SelectedIndex = 1;
            ((CheckBox)nativeView.FindName("NativePacketDropsBox")).IsChecked = true;
            nativeView.SetActive(true); Complete(nativeView.RefreshAsync());
            Check(nativeSource.LastOptions is { Minutes: 60, IncludePacketDrops: true }, "R34 actual UI passes selected interval and packet-drop opt-in to its source");
            nativeSource.Snapshot = nativeSource.Snapshot with { Available = false, Partial = true, Events = [], Note = "fixture access denied" };
            nativeView.SetActive(true); Complete(nativeView.RefreshAsync());
            Check(nativeGrid.Items.Count == 0 && ((SelectableText)nativeView.FindName("NativeNetworkStatusText")).Text.Contains("N/D") &&
                ((SelectableText)nativeView.FindName("NativeNetworkCoverageText")).Text.Contains("fixture access denied"),
                "R16 unavailable log clears old rows and preserves error without invented zero");
            nativeSource.Snapshot = nativeSource.Snapshot with { Available = true, Partial = false, Events = Array.Empty<NativeNetworkEvent>() };
            Complete(nativeView.RefreshAsync());
            Check(((SelectableText)nativeView.FindName("NativeNetworkStatusText")).Text.Contains("Copertura incompleta") &&
                ((SelectableText)nativeView.FindName("NativeNetworkCoverageText")).Text.Contains("Audit disabilitato") &&
                ((SelectableText)nativeView.FindName("NativeNetworkSetupText")).Text.Contains("/failure:enable"),
                "R18 screenshot disabled audit is prominent with optional manual setup and no fabricated events");
            nativeView.SetActive(false);

        }
        finally
        {
            try { source.Release.Set(); if (Directory.Exists(root)) Directory.Delete(root, true); }
            finally { SynchronizationContext.SetSynchronizationContext(previousContext); }
        }
        Console.WriteLine("Network UI fixtures complete; no window shown, live network acquisition or real clipboard write.");
    }
    private sealed class NativeFixture : INativeNetworkEvidenceQuerySource, IDisposable
    {
        public int Calls; public bool Block;
        public NetworkEventReadOptions? LastOptions;
        public NativeNetworkSnapshot Capture(NetworkEventReadOptions options, CancellationToken cancellationToken)
        { LastOptions = options; return Capture(cancellationToken) with { Options = options, SinceUtc = Snapshot.CapturedUtc.AddMinutes(-options.Minutes) }; }
        public readonly ManualResetEventSlim Started = new(false), Release = new(true);
        public NativeNetworkSnapshot Snapshot = new(DateTime.UtcNow, DateTime.UtcNow.AddMinutes(-15), true, false,
            new[] { new NetworkAuditSetting("WFP", false, "fixture policy") },
            new[] { new NativeNetworkEvent(DateTime.UtcNow, 1, 5157, "Connessione bloccata da WFP", "192.0.2.8", "12345", "192.0.2.1", "3389", "Ingresso", "TCP", 123, "fixture.exe", "Blocco registrato") }, "fixture note");
        public NativeNetworkSnapshot Capture(CancellationToken cancellationToken)
        { Interlocked.Increment(ref Calls); Started.Set(); if (Block && !Release.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException(); return Snapshot; }
        public void Dispose() { Release.Set(); Started.Dispose(); Release.Dispose(); }
    }
    private sealed class PacketFixture : IPacketCaptureClient
    {
        public int Calls; public bool LastMonitor;
        public TaskCompletionSource<PacketEvidence>? Block;
        public static readonly PacketEvidence Evidence = new(1, 1, false, new[] {
            new PacketObservation("Trasmettitore nel frame", "02-11-22-33-44-55", "N/D", "N/D", "N/D", "N/D", 1) }, "Fixture only.");
        public Task<CaptureDiscovery> DiscoverAsync(string? path, CancellationToken token)
        {
            Calls++; return Task.FromResult(new CaptureDiscovery(new("dumpcap.exe", "tshark.exe", "Wireshark.exe", "fixture"),
                new[] { new CaptureInterface(@"\Device\NPF_{00112233-4455-6677-8899-AABBCCDDEEFF}", "Wi-Fi") }, "fixture"));
        }
        public Task<CaptureCheck> CheckMonitorAsync(CaptureTools tools, CaptureInterface adapter, CancellationToken token)
        { Calls++; return Task.FromResult(new CaptureCheck(true, "fixture link type")); }
        public Task<CaptureFileResult> CaptureAsync(CaptureTools tools, CaptureInterface adapter, bool monitor, string path, CancellationToken token)
        { Calls++; LastMonitor = monitor; return Task.FromResult(new CaptureFileResult(path, true, "fixture capture")); }
        public Task<PacketEvidence> AnalyzeAsync(CaptureTools tools, string path, CancellationToken token)
        { Calls++; return Block?.Task ?? Task.FromResult(Evidence); }
    }
    private sealed class FixtureSource : INetworkDeviceSource, IDisposable
    {
        public int Calls;
        public readonly ManualResetEventSlim Started = new(false), Release = new(true);
        public NetworkDeviceSnapshot Snapshot = new(DateTime.UnixEpoch, true, false, "", 1, new[] {
            new NetworkNeighbor("adapter", "Wi-Fi", NetworkDeviceRules.NetworkKey("adapter", new[] { ("192.168.1.1", "02-AA-BB-CC-DD-EE") }),
                "192.168.1.2", "02-11-22-33-44-55", NeighborState.Reachable, false) });
        public void Block() { Started.Reset(); Release.Reset(); }
        public NetworkDeviceSnapshot Capture()
        {
            Interlocked.Increment(ref Calls); Started.Set();
            if (!Release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException("Fixture release timeout.");
            return Snapshot;
        }
        public void Dispose() { Release.Set(); Started.Dispose(); Release.Dispose(); }
    }
}

sealed class NativeCaptureFixture : INativeCaptureClient, IDisposable
{
    public int Calls, Captures, Released;
    public readonly ManualResetEventSlim Started = new();
    public bool WifiFailure;
    public TaskCompletionSource<NativeWifiResult>? Pending;
    public static readonly NativeWifiResult Wifi = new(true, false, new[] { new WifiBssRow("Wi-Fi", "Rete fixture", "02-11-22-33-44-55", -42, 2412000, "Punto di accesso") }, "cache fixture");
    public NativeCaptureDiscovery Discover(CancellationToken cancellation)
    { Interlocked.Increment(ref Calls); return new(true, new[] { new NativeCaptureSource("fixture", "Scheda fixture") }, "fixture"); }
    public NativeCaptureResult Capture(NativeCaptureSource source, CancellationToken cancellation)
    { Interlocked.Increment(ref Calls); Captures++; return new(true, false, new[] { new NativePacketRow("12:00:00.000", "TCP", "192.0.2.1:123", "192.0.2.2:443", "Out", 40, 1, 0) }, 0, 0, "fixture"); }
    public NativeWifiResult ReadWifi(CancellationToken cancellation)
    { Interlocked.Increment(ref Calls); if (Pending is not null) Started.Set(); return Pending?.Task.GetAwaiter().GetResult() ?? (WifiFailure ? new(false, true, Array.Empty<WifiBssRow>(), "fixture access denied") : Wifi); }
    public void Release() => Released++;
    public void Dispose() => Started.Dispose();
}
