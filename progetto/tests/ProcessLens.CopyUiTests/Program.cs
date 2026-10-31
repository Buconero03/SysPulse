using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using ProcessLens.App;
using ProcessLens.Core;
using ProcessLens.Windows;

internal static class Program
{
    private static void Check(bool ok, string label)
    {
        if (!ok) throw new Exception("FAIL copy UI: " + label);
        Console.WriteLine("PASS copy UI: " + label);
    }

    private static void DrainLayoutWork(Dispatcher dispatcher, TimeSpan remaining)
    {
        dispatcher.VerifyAccess();
        if (remaining <= TimeSpan.Zero) throw new TimeoutException("Text layout exceeded its five-second test budget.");
        var frame = new DispatcherFrame();
        bool expired = false;
        // WPF TextBoxView schedules its continuation at Background priority. ContextIdle
        // runs after that work, unlike a Measure-only check or a higher-priority sentinel.
        DispatcherOperation sentinel = dispatcher.BeginInvoke(DispatcherPriority.ContextIdle,
            new Action(() => frame.Continue = false));
        var deadline = new DispatcherTimer(DispatcherPriority.Send, dispatcher) { Interval = remaining };
        EventHandler onDeadline = (_, _) => { expired = true; frame.Continue = false; };
        deadline.Tick += onDeadline;
        try
        {
            deadline.Start();
            Dispatcher.PushFrame(frame);
        }
        finally
        {
            deadline.Stop();
            deadline.Tick -= onDeadline;
            if (sentinel.Status == DispatcherOperationStatus.Pending) sentinel.Abort();
        }
        if (expired) throw new TimeoutException("Text layout Dispatcher drain exceeded its five-second test budget.");
    }

    private static Size LayoutOutput(SelectableText text, double width)
    {
        text.Dispatcher.VerifyAccess();
        text.Width = width;
        var watch = System.Diagnostics.Stopwatch.StartNew();
        Size previous = default, first = default;
        int previousLines = -1, passes = 0;
        bool settled = false;
        // Wait for stable layout across queue drains, not for the height assertion to pass.
        // No fixed sleep, artificial MinHeight/MinLines or replacement native template.
        for (; passes < 16 && watch.Elapsed < TimeSpan.FromSeconds(5); passes++)
        {
            text.Measure(new Size(width, double.PositiveInfinity));
            if (passes == 0) first = text.DesiredSize;
            text.Arrange(new Rect(new Point(), text.DesiredSize));
            text.UpdateLayout();
            DrainLayoutWork(text.Dispatcher, TimeSpan.FromSeconds(5) - watch.Elapsed);
            // The background callback invalidates the view. Re-measure the complete tree.
            text.Measure(new Size(width, double.PositiveInfinity));
            text.Arrange(new Rect(new Point(), text.DesiredSize));
            text.UpdateLayout();
            Size current = text.DesiredSize;
            int lines = text.LineCount;
            if (text.IsMeasureValid && text.IsArrangeValid && lines >= 0 &&
                current == previous && lines == previousLines)
            { settled = true; break; }
            previous = current; previousLines = lines;
        }
        string state = FormattableString.Invariant(
            $"initialized={text.IsInitialized}, template={text.Template is not null}, first={first.Width:F2}x{first.Height:F2}, desired={text.DesiredSize.Width:F2}x{text.DesiredSize.Height:F2}, lines={text.LineCount}, passes={passes + 1}");
        Check(settled, "text layout settles within bounded Dispatcher passes; " + state);
        Check(text.IsInitialized && text.Template?.FindName("PART_ContentHost", text) is FrameworkElement,
            "native text template is initialized; " + state);
        // Keep the original finite-height assertion; an absent template must fail, not be masked.
        Check(double.IsFinite(text.DesiredSize.Height) && text.DesiredSize.Height > 20,
            "multiline output measures at finite height; " + state);
        Check(text.IsMeasureValid && text.IsArrangeValid && text.LineCount >= 3,
            "multiline output has arranged text lines; " + state);
        return text.DesiredSize;
    }

    [STAThread]
    public static void Main()
    {
        _ = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        // Independent read-only binding check on this test process. No target is launched or terminated.
        using (var self = System.Diagnostics.Process.GetCurrentProcess())
        {
            var selfIdentity = ProcessExitIdentity.From(self.Id, self.StartTime, self.ProcessName)!;
            int exits = 0;
            using var selfWatch = ProcessExitWatch.TryCreate(selfIdentity, _ => Interlocked.Increment(ref exits));
            Check(selfWatch is not null, "read-only Windows exit watch binds to this test process");
            using var wrongLifetime = ProcessExitWatch.TryCreate(selfIdentity with {
                StartUtcTicks = selfIdentity.StartUtcTicks + TimeSpan.TicksPerDay
            }, _ => Interlocked.Increment(ref exits));
            Check(wrongLifetime is null, "native handle rejects a different creation timestamp for the same PID");
            selfWatch!.Dispose();
            selfWatch.Dispose();
            Check(Volatile.Read(ref exits) == 0, "disposing a live-process watch is idempotent and does not report an exit");
        }
        // Native CPU read is cross-checked against .NET's independent process-time reader.
        using (var self = System.Diagnostics.Process.GetCurrentProcess())
        {
            var cpuSource = new WindowsLiveCpuSampler();
            double lower = self.TotalProcessorTime.TotalSeconds;
            var baseline = cpuSource.Capture(new[] { self.Id, int.MaxValue });
            self.Refresh();
            double upper = self.TotalProcessorTime.TotalSeconds;
            var sample = baseline.Processes[self.Id];
            Check(sample.CounterAvailable && !sample.Available && sample.StartUtc == self.StartTime.ToUniversalTime(),
                "native CPU first sample verifies process creation and remains unavailable as a rate");
            Check(sample.CumulativeSeconds >= lower - 0.000001 && sample.CumulativeSeconds <= upper + 0.000001,
                "native cumulative CPU is bracketed by independent .NET counter reads");
            Check(!baseline.Processes[int.MaxValue].CounterAvailable && !baseline.Processes[int.MaxValue].Available,
                "nonexistent PID is unavailable, not sampled zero CPU");
            System.Threading.Thread.Sleep(100);
            var next = cpuSource.Capture(new[] { self.Id }).Processes[self.Id];
            Check(next.Available && next.IntervalSeconds > 0 && next.CapturedAtMonotonicSeconds > 0 && next.Percent is >= 0 and <= 100,
                "native CPU second read yields bounded rate over actual interval");
            cpuSource.Reset();
            Check(!cpuSource.Capture(new[] { self.Id }).Processes[self.Id].Available,
                "native CPU reset after minimize requires a new pair of counters");
            var resources = WindowsSelfResources.Capture();
            Check(resources.Pid == self.Id && resources.Resident.HasValue && resources.Resident.Value > 0 &&
                resources.PrivateCommit.HasValue && resources.PrivateCommit.Value > 0 &&
                (!resources.PrivateResident.HasValue || resources.PrivateResident.Value <= resources.Resident!.Value),
                "GUI memory counters distinguish private resident, total resident and commit");
        }
        var rowProcess = new ProcessSnapshot { Pid = 123, ParentPid = 0, Name = "sample.exe", StartTime = DateTime.UnixEpoch };
        var rowInsight = new ProcessLens.Semantics.ProcessIntelligenceAnalyzer().Analyze(rowProcess);
        var liveRow = new ProcessRow(rowProcess, rowInsight);
        var notifications = new List<string?>();
        liveRow.PropertyChanged += (_, e) => notifications.Add(e.PropertyName);
        var validSample = new LiveCpuSample(123, DateTime.UnixEpoch, true, 12.5, 10, 15, 2, 20, true, 8);
        liveRow.UpdateCpu(validSample);
        Check(liveRow.Process.CpuSampleAvailable && liveRow.CpuPercent == 12.5 && notifications.Contains("CpuDisplay"),
            "CPU row update notifies existing bindings without replacing the row");
        liveRow.UpdateCpu(validSample with { StartUtc = DateTime.UnixEpoch.AddTicks(1) });
        Check(!liveRow.Process.CpuSampleAvailable && liveRow.CpuDisplay == "N/D",
            "reused PID cannot acquire CPU from a different process instance");
        string copied = "";
        var text = new SelectableText(value => copied = value);
        // Standalone WPF controls have no parent/XAML loader to complete initialization.
        // EndInit completes initialization; LayoutOutput also drains deferred native formatting.
        text.BeginInit();
        text.Text = "VirtualQueryEx\r\nAPI: perché 🧪\r\nN/D non equivale a zero.";
        text.Width = 210;
        text.FontSize = 13;
        text.EndInit();
        text.IsReadOnly = false;
        Check(text.IsReadOnly && !text.IsUndoEnabled, "output stays read-only and has no undo history");
        Check(!ApplicationCommands.Copy.CanExecute(null, text), "copy selection disabled when nothing selected");
        int begin = text.Text.IndexOf("perché", StringComparison.Ordinal);
        text.Select(begin, "perché 🧪".Length);
        Check(ApplicationCommands.Copy.CanExecute(null, text), "native Copy command enabled for selection");
        ApplicationCommands.Copy.Execute(null, text);
        Check(copied == "perché 🧪", "Copy command returns exactly selected Unicode text");
        SelectableText.CopyAllCommand.Execute(null, text);
        Check(copied == text.Text, "Copy all returns whole block without changing selection");
        Check(text.SelectionStart == begin && text.SelectionLength == "perché 🧪".Length, "copy does not rewrite selection");
        Size wide = LayoutOutput(text, 210);
        int wideLines = text.LineCount;
        Size narrow = LayoutOutput(text, 70);
        Check(narrow.Height > wide.Height && text.LineCount > wideLines,
            "narrow output wraps into more lines and grows vertically");
        Check(text.Text == "VirtualQueryEx\r\nAPI: perché 🧪\r\nN/D non equivale a zero." &&
            text.SelectionStart == begin && text.SelectionLength == "perché 🧪".Length,
            "layout and wrapping preserve original text and selection");
        ApplicationCommands.Copy.Execute(null, text);
        Check(copied == "perché 🧪", "copy selection remains exact after layout");

        // Deterministic queue regression: the old Measure-only helper sees one line
        // here even with warm font caches. No clipboard, collection or hidden window.
        var deferred = new SelectableText(_ => { });
        deferred.BeginInit(); deferred.Text = "prima riga"; deferred.FontSize = 13; deferred.EndInit();
        bool backgroundRan = false;
        deferred.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
        {
            deferred.Text = "prima riga\r\nseconda riga\r\nterza riga";
            backgroundRan = true;
        }));
        Size deferredSize = LayoutOutput(deferred, 210);
        Check(backgroundRan && deferred.LineCount >= 3 && deferredSize.Height > 20,
            "queued Background text is included before multiline assertions");

        int built = 0;
        var chapter = ReportView.Section("01 · ALLOCAZIONI WINDOWS — PROVE NATIVE E PICCHI STORICI", () =>
        {
            built++;
            return ReportView.Body("VirtualQueryEx: 824,7 MiB virtuali impegnati.\nQueryWorkingSetEx: campione parziale.");
        });
        Check(chapter.Content is null && built == 0, "closed chapter does not allocate text controls");
        chapter.BeginInit();
        chapter.EndInit();
        chapter.Width = 360;
        chapter.Measure(new Size(360, double.PositiveInfinity));
        chapter.Arrange(new Rect(new Point(), chapter.DesiredSize));
        chapter.UpdateLayout();
        var chapterHeader = (TextBlock)chapter.Header;
        double wideHeaderHeight = chapterHeader.ActualHeight;
        chapter.Width = 180;
        chapter.Measure(new Size(180, double.PositiveInfinity));
        chapter.Arrange(new Rect(new Point(), chapter.DesiredSize));
        chapter.UpdateLayout();
        Check(chapterHeader.TextWrapping == TextWrapping.Wrap && chapterHeader.MaxWidth <= 180 &&
            wideHeaderHeight > 0 && chapterHeader.ActualHeight > wideHeaderHeight,
            "long native chapter title wraps at narrow detail-pane width");
        chapter.IsExpanded = true;
        Check(built == 1 && chapter.Content is StackPanel panel && panel.Children.Count == 2,
            "opening chapter builds separate API blocks on demand");
        chapter.IsExpanded = false;
        Check(chapter.Content is null, "closing chapter releases generated blocks");
        chapter.IsExpanded = true;
        Check(built == 2, "reopened chapter recreates its current content");

        var auditHost = new StackPanel();
        var auditView = new AuditLogView(auditHost);
        const string firstEvent = "[23:31:34.149] [VERIFICA RAM] INIZIO — Richiesta esplicita.";
        auditView.Render(AuditLogPresentation.Parse(firstEvent));
        var firstCard = (Border)auditHost.Children[0];
        var firstContent = (StackPanel)firstCard.Child;
        var messageText = (SelectableText)firstContent.Children[1];
        Check(firstCard.Margin.Bottom == 6 && firstCard.Padding.Left == 10 && messageText.FontSize == 12.5 &&
            messageText.TextWrapping == TextWrapping.Wrap, "actual audit card uses compact spacing and readable wrapped text");
        messageText.Select(0, 9);
        auditView.Render(AuditLogPresentation.Parse(firstEvent + "\n[23:31:34.506] [VERIFICA RAM] PSAPI — 44,0 MiB"));
        Check(auditHost.Children.Count == 2 && ReferenceEquals(auditHost.Children[0], firstCard) && messageText.SelectionLength == 9,
            "appended audit event preserves existing controls and text selection");
        auditView.Render(AuditLogPresentation.Parse("[23:32:00.000] [VERIFICA RAM] INIZIO — Nuova analisi."));
        Check(auditHost.Children.Count == 1 && !ReferenceEquals(auditHost.Children[0], firstCard),
            "new audit replaces old entries without mixing analyses");
        auditView.Clear();
        Check(auditHost.Children.Count == 0, "collapsing audit releases generated cards");

        var saved = CultureInfo.CurrentCulture;
        try
        {
            foreach (string cultureName in new[] { "it-IT", "en-US" })
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
                var grid = new DataGrid { AutoGenerateColumns = false, IsReadOnly = true, CanUserAddRows = false };
                grid.Columns.Add(new DataGridTextColumn { Header = "Nome", Binding = new Binding(nameof(Row.Name)) });
                grid.Columns.Add(new DataGridTextColumn { Header = "RAM", Binding = new Binding(nameof(Row.MiB))
                    { StringFormat = "{0:F1} MiB", ConverterCulture = CultureInfo.CurrentCulture } });
                grid.Columns.Add(new DataGridTextColumn { Header = "Nascosto", Binding = new Binding(nameof(Row.Hidden)), Visibility = Visibility.Collapsed });
                var first = new Row("primo", 26, "DO_NOT_COPY");
                grid.ItemsSource = new[] { first, new Row("secondo", 12, "DO_NOT_COPY") };
                grid.SelectedItem = first;
                string snapshot = GridCopySupport.CaptureTable(grid, true);
                Check(snapshot.Contains("primo\t" + 26d.ToString("F1") + " MiB", StringComparison.Ordinal) &&
                    !snapshot.Contains("secondo", StringComparison.Ordinal) && !snapshot.Contains("DO_NOT_COPY", StringComparison.Ordinal),
                    "selected-row copy uses formatted visible columns " + cultureName);
                Check(ReferenceEquals(grid.SelectedItem, first), "copy does not retarget row actions " + cultureName);
                grid.ItemsSource = new[] { new Row("replacement", 99, "HIDDEN") };
                Check(!snapshot.Contains("replacement", StringComparison.Ordinal), "copied snapshot survives source refresh " + cultureName);
            }
        }
        finally { CultureInfo.CurrentCulture = saved; }

        // Construct the real XAML, without Show/Run/SetActive: no process refresh, SQLite read or profiler capture.
        var window = new MainWindow();
        Check(!window.IsLoaded && window.FindName("ProcessQueryAnswerText") is SelectableText &&
            window.FindName("ServiceHistoryStatusText") is SelectableText, "actual window XAML has selectable analysis and log controls");
        Check(window.FindName("ProcessQueryOverviewLabel") is SelectableText &&
            window.FindName("DetailEvidenceOverviewText") is SelectableText,
            "actual query and evidence panels contain initial overview controls");
        Check(window.FindName("ProcessQueryAuditEntriesPanel") is StackPanel &&
            window.FindName("ProcessQueryAuditText") is SelectableText,
            "actual audit markup retains structured entries and original selectable source");
        var actualAuditPanel = (Expander)window.FindName("ProcessQueryAuditPanel");
        var actualAuditRaw = (SelectableText)window.FindName("ProcessQueryAuditText");
        var actualAuditEntries = (StackPanel)window.FindName("ProcessQueryAuditEntriesPanel");
        actualAuditPanel.Visibility = Visibility.Visible;
        actualAuditRaw.Text = firstEvent;
        Check(actualAuditEntries.Children.Count == 1 && ((Button)window.FindName("CopyQueryAuditButton")).IsEnabled,
            "real audit TextChanged event renders cards and enables original-log copy");
        actualAuditPanel.IsExpanded = false;
        Check(actualAuditEntries.Children.Count == 0, "real audit collapse handler releases cards");
        actualAuditPanel.IsExpanded = true;
        Check(actualAuditEntries.Children.Count == 1, "real audit expansion restores current entries");
        actualAuditRaw.Text = "";
        Check(actualAuditEntries.Children.Count == 0 && !((Button)window.FindName("CopyQueryAuditButton")).IsEnabled,
            "real audit reset clears old entries and disables stale-log copy");
        actualAuditPanel.Visibility = Visibility.Collapsed;
        var noticeCard = (Border)window.FindName("ProcessNoticeCard");
        Check(noticeCard.Visibility == Visibility.Collapsed && noticeCard.MaxWidth == 420 &&
            window.FindName("ProcessNoticeText") is SelectableText,
            "exit notice starts hidden, stays compact and its text can be copied");
        var processWorkspace = (Grid)window.FindName("ProcessWorkspace");
        var initialListWidth = processWorkspace.ColumnDefinitions[0].Width;
        var focusButton = (Button)window.FindName("ToggleDetailsFocusButton");
        focusButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(((Border)window.FindName("ProcessListPanel")).Visibility == Visibility.Collapsed &&
            processWorkspace.ColumnDefinitions[0].Width.Value == 0 &&
            ((GridSplitter)window.FindName("ProcessDetailsSplitter")).Visibility == Visibility.Collapsed,
            "detail focus gives the existing analysis pane all available width without another window");
        focusButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(((Border)window.FindName("ProcessListPanel")).Visibility == Visibility.Visible &&
            processWorkspace.ColumnDefinitions[0].Width == initialListWidth &&
            processWorkspace.ColumnDefinitions[0].MinWidth == 420,
            "return restores list width and minimum width after detail focus");
        Check(((Expander)window.FindName("GuiResourcesExpander")).IsExpanded == false &&
            ((Expander)window.FindName("WindowsComponentContextExpander")).IsExpanded == false,
            "secondary self-resource counters and Windows reference start collapsed");
        var memory = new MemoryDashboardView();
        Check(memory.FindName("CategoryServicesCard") is Button { Tag: "Secondario" } &&
            memory.FindName("CategoryAppsCard") is Button && memory.FindName("ResidualCategoryDetails") is Expander,
            "RAM category cards are keyboard-accessible buttons and residual opens limitations");
        var serviceFixture = new ServiceSnapshot { Name = "fixture", DisplayName = "Servizio di prova", StateCode = 4,
            ProcessId = 12, ServiceType = 0x10, Description = "Descrizione dichiarata" };
        var serviceDetails = new ServiceDetailsWindow(serviceFixture);
        Check(serviceDetails.Content is ScrollViewer && serviceDetails.Title.Contains("Servizio di prova"),
            "service details window builds read-only chapters without service actions");
        serviceDetails.Close();
        var guardian = new GuardianDashboardView();
        Check(memory.FindName("RefreshStatus") is SelectableText && guardian.FindName("StatusText") is SelectableText,
            "actual dashboard XAML has selectable status output");
        Check(GridCopySupport.GetEnabled((DataGrid)guardian.FindName("EventsTable")) &&
            GridCopySupport.GetEnabled((DataGrid)memory.FindName("TopMemoryProcesses")), "actual log/RAM tables have copy support");
        var diagnostics = (DataGrid)guardian.FindName("DiagnosticsTable");
        var diagnostic = GuardianDiagnosticPresentation.From(new("20/10/2026 13:06:05", "health",
            "accepted=2 saved=1 working_set_mib=57,7"));
        diagnostics.ItemsSource = new[] { diagnostic };
        diagnostics.SelectedItem = diagnostic;
        var detailBinding = BindingOperations.GetBindingExpression((SelectableText)guardian.FindName("DiagnosticDetailsText"), TextBox.TextProperty);
        var originalBinding = BindingOperations.GetBindingExpression((SelectableText)guardian.FindName("DiagnosticOriginalText"), TextBox.TextProperty);
        detailBinding?.UpdateTarget();
        originalBinding?.UpdateTarget();
        Check(detailBinding is not null && originalBinding is not null &&
            ((SelectableText)guardian.FindName("DiagnosticDetailsText")).Text == diagnostic.DetailText &&
            ((SelectableText)guardian.FindName("DiagnosticOriginalText")).Text == diagnostic.OriginalMessage,
            "Guardian selection binds translated details and exact original log to selectable fields");
        window.Close();
        Console.WriteLine("Copy UI regressions complete; OS clipboard not modified, no GUI shown or collection started.");
    }

    public sealed record Row(string Name, double MiB, string Hidden);
}
