using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Threading;
using ProcessLens.App;
using ProcessLens.Core;

internal static class Program
{
    static void Check(bool ok, string label)
    { if (!ok) throw new Exception("FAIL system UI: " + label); Console.WriteLine("PASS system UI: " + label); }
    static void Complete(Task task)
    {
        var frame = new DispatcherFrame(); var dispatcher = Dispatcher.CurrentDispatcher;
        bool timedOut = false;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
        timer.Tick += (_, _) => { timedOut = true; frame.Continue = false; };
        task.ContinueWith(_ => dispatcher.BeginInvoke(new Action(() => frame.Continue = false)), TaskScheduler.Default);
        timer.Start(); Dispatcher.PushFrame(frame); timer.Stop();
        if (timedOut) throw new Exception("UI fixture timed out");
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
        try
        {
            Check(SynchronizationContext.Current is DispatcherSynchronizationContext,
                "R14 R3 WPF context installed before starting asynchronous operations");
            var completion = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            Task continuation = VerifyDispatcherContinuationAsync(completion.Task, dispatcher, ownerThread);
            // Queue only after the incomplete task has been awaited, guaranteeing an actual asynchronous continuation.
            ThreadPool.QueueUserWorkItem(_ => completion.SetResult(Environment.CurrentManagedThreadId));
            Complete(continuation);
            using var source = new Fixture();
            var view = new SystemProcessesView(source, @"C:\Windows");
            Check(source.Calls == 0, "constructor performs no native inventory");
            view.SetActive(true); Check(source.Calls == 0, "activation does not automatically query Windows");
            Complete(view.RefreshAsync());
            var grid = (DataGrid)view.FindName("SystemProcessesTable");
            var analyze = (Button)view.FindName("AnalyzeSystemButton");
            Check(grid.IsReadOnly && GridCopySupport.GetEnabled(grid) && grid.EnableRowVirtualization && grid.Items.Count == 3,
                "R15 component table retains internal entries and groups service hosts without applications");
            Check(GridCopySupport.CaptureTable(grid, false).Contains("Memory Compression"), "actual grid copy preserves internal process names");
            var coverage = (SelectableText)view.FindName("SystemCoverageText");
            Check(coverage.Text.Contains("fonte: 5") && coverage.Text.Contains("istanze 4 · gruppi 3") &&
                coverage.Text.Contains("Fuori catalogo: 1"),
                "R22 visible coverage keeps full enumeration distinct from grouped Windows instances");
            grid.SelectedItem = grid.Items.Cast<WindowsComponentGroup>().Single(r => r.Name == "Memory Compression");
            Check(!analyze.IsEnabled && ((SelectableText)view.FindName("SystemSelectionText")).Text.Contains("dimensione logica"),
                "unknown creation time blocks analysis navigation without hiding compression details");
            var scope = (ComboBox)view.FindName("SystemScopeBox");
            Check(scope.Items.Count == 6 && grid.Items.Cast<WindowsComponentGroup>().All(g => g.Name != "chrome.exe"),
                "R15 only component categories; ordinary applications absent even under Windows");
            scope.SelectedIndex = 1;
            Check(grid.Items.Count == 2, "R15 kernel scope retains internal component groups");
            scope.SelectedIndex = 3;
            Check(grid.Items.Count == 1 && ((WindowsComponentGroup)grid.Items[0]).Count == 2,
                "R15 service scope groups two hosts in one row");
            ((TextBox)view.FindName("SystemSearchBox")).Text = "12";
            Check(grid.Items.Count == 1 && ((WindowsComponentGroup)grid.Items[0]).Instances.Single().Pid == 12, "numeric PID search does not match substring 112");
            Check(((SelectableText)view.FindName("SystemStatusText")).Text.Contains("gruppi mostrati: 1 · istanze mostrate: 1") &&
                ((SelectableText)view.FindName("SystemStatusText")).Text.Contains("filtri attivi") && coverage.Text.Contains("fonte: 5"),
                "R22 filter changes visible instances without rewriting enumeration coverage");
            grid.SelectedIndex = 0; SystemProcessEntry? request = null; view.AnalysisRequested += p => request = p;
            analyze.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(request?.Pid == 12 && request.StartUtc.HasValue, "analysis button forwards selected immutable process identity");
            ((TextBox)view.FindName("SystemSearchBox")).Clear();
            grid.SelectedIndex = 0;
            var instances = (ComboBox)view.FindName("SystemInstancesBox");
            Check(instances.Items.Count == 2, "R15 grouped component exposes each actual PID for deeper analysis");
            instances.SelectedIndex = 1; analyze.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(request?.Pid == 112, "R15 instance selector forwards the chosen PID rather than the first in its group");
            source.Block = true; Task pending = view.RefreshAsync();
            Check(source.Started.Wait(TimeSpan.FromSeconds(5)), "blocked native fixture begins");
            Complete(view.RefreshAsync()); Check(source.Calls == 2 && grid.Items.Count == 0, "refresh coalesces and clears old rows while pending");
            view.SetActive(false); source.Release.Set(); Complete(pending);
            Check(grid.Items.Count == 0 && !analyze.IsEnabled && ((SelectableText)view.FindName("SystemMemoryText")).Text.Contains("N/D"),
                "late uncancellable result rejected after section closes and rows released");
            Check(coverage.Text.Contains("N/D"), "R22 deactivation clears previous coverage");
            view.SetActive(true); source.Block = false; source.Fail = true; Complete(view.RefreshAsync());
            Check(grid.Items.Count == 0 && ((SelectableText)view.FindName("SystemSourceText")).Text.Contains("synthetic failure"),
                "acquisition error preserves failure instead of a valid empty inventory");
            Check(coverage.Text.Contains("N/D"), "R22 acquisition failure cannot retain old enumeration counts");
            view.SetActive(false);
            var window = new MainWindow();
            var ram = (SelectableText)window.FindName("SystemRamText");
            Check(ram.Text == "In attesa…", "R19 header RAM starts pending without a fabricated zero");
            window.UpdateSystemRam(new SystemMemoryEvidence(16UL * 1073741824, 6UL * 1073741824));
            Check(ram.Text.Contains("GiB") && ram.Text.Contains("62") && ram.Text.Contains("%") &&
                ram.ToolTip.ToString()!.Contains("GlobalMemoryStatusEx"),
                "R19 global header publishes physical RAM amount percentage and source");
            window.UpdateSystemRam(null);
            Check(ram.Text == "N/D", "R19 failed RAM sample clears the previous value");
            window.SetDashboardWindowVisibility(false);
            Check(ram.Text == "Sospeso", "R19 minimized window marks RAM sampling suspended");
            var host = (ContentControl)window.FindName("SystemProcessesHost");
            Check(host.Content is null, "main window leaves system pane unconstructed");
            ((Button)window.FindName("NavSystemButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(host.Content is SystemProcessesView && host.Visibility == Visibility.Visible &&
                ((Grid)window.FindName("ProcessWorkspace")).Visibility == Visibility.Collapsed, "System Windows button opens a dedicated lazy workspace");
            Check(((StackPanel)window.FindName("ProcessDetectedCounter")).Visibility == Visibility.Collapsed &&
                ((StackPanel)window.FindName("ProcessVisibleCounter")).Visibility == Visibility.Collapsed,
                "R15 ordinary process counters hidden in system workspace");
            ((Button)window.FindName("NavNetworkButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(host.Visibility == Visibility.Collapsed, "switching to network deactivates system workspace");
            window.Close(); Application.Current.Shutdown();
            Console.WriteLine("System process WPF tests complete; no real native query, process action or clipboard used.");
        }
        finally { SynchronizationContext.SetSynchronizationContext(previousContext); }
    }
    sealed class Fixture : ISystemProcessSource, IDisposable
    {
        public int Calls; public bool Block, Fail;
        public ManualResetEventSlim Started = new(false), Release = new(false);
        public SystemProcessCatalog Capture(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref Calls);
            if (Block) { Started.Set(); if (!Release.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException(); }
            if (Fail) throw new InvalidOperationException("synthetic failure");
            return new(DateTime.UtcNow, true, false, "synthetic source", "unchanged source note",
                new[] { new SystemProcessEntry(0, 0, "System Idle Process", 4, 0, 0, null),
                    new SystemProcessEntry(1120, 4, "Memory Compression", 4, 3, 0, 1048576, Access: "Win32 5"),
                    new SystemProcessEntry(12, 4, "svchost.exe", 1, 3, 0, 1048576,
                        @"C:\Windows\System32\svchost.exe", new DateTime(2026, 10, 21, 12, 0, 0, DateTimeKind.Utc), "Percorso e avvio letti"),
                    new SystemProcessEntry(112, 4, "svchost.exe", 1, 3, 0, 2097152,
                        @"C:\Windows\System32\svchost.exe", new DateTime(2026, 10, 21, 12, 0, 1, DateTimeKind.Utc), "Percorso e avvio letti"),
                    new SystemProcessEntry(1200, 4, "chrome.exe", 1, 3, 1, 0, @"C:\Windows\chrome.exe") },
                new(8L * 1024 * 1048576, 2L * 1024 * 1048576, null, null, null, null, "synthetic memory"));
        }
        public void Dispose() { Started.Dispose(); Release.Dispose(); }
    }
}
