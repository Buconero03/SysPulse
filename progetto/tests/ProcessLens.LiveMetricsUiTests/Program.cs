using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using ProcessLens.App;
using ProcessLens.Core;
using ProcessLens.Semantics;

internal static class Program
{
    static void Check(bool ok, string label)
    { if (!ok) throw new Exception("FAIL R24 WPF " + label); Console.WriteLine("PASS R24 WPF " + label); }
    static void Set(object target, string field, object value) =>
        typeof(MainWindow).GetField(field, BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(target, value);
    static void Drain()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }
    [STAThread]
    public static void Main()
    {
        _ = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var main = new MainWindow(); // no Loaded, no polling, no tray registration.
        var start = DateTime.UtcNow.AddMinutes(-1);
        var process = new ProcessSnapshot { Pid = 42, ParentPid = 1, Name = "fixture.exe", StartTime = start,
            PrivateWorkingSetBytes = 900, PrivateWorkingSetAvailable = true, WorkingSetBytes = 1000, WorkingSetAvailable = true };
        var row = new ProcessRow(process, new ProcessIntelligenceAnalyzer().Analyze(process));
        var list = new List<ProcessRow> { row };
        var processList = (ListBox)main.FindName("ProcessList"); processList.ItemsSource = list;
        Set(main, "_allRows", list); Set(main, "_currentDetailRow", row); Set(main, "_selectedPid", (int?)42!);
        var changes = new HashSet<string>(); row.PropertyChanged += (_, e) => changes.Add(e.PropertyName!);
        double now = Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
        var sample = new LiveMemorySample(42, start, 4 * 1048576, true, 2 * 1048576, true, 9 * 1048576, true, now, DateTime.UtcNow);
        main.ApplyLiveMemorySamples(new Dictionary<int, LiveMemorySample> { [42] = sample }); Drain();
        Check(ReferenceEquals(processList.ItemsSource, list) && ReferenceEquals(list[0], row), "refresh retains list and row identity");
        Check(row.MemoryDisplayMb == 2 && changes.Contains("MemoryDisplayText") && changes.Contains("MemoryShortLabel"), "bindings notified without metadata rebuild");
        Check(((SelectableText)main.FindName("DetailMemoryText")).Text == row.MemoryDisplayText &&
            ((SelectableText)main.FindName("DetailPrivateCommitText")).Text.Contains("9"), "selected detail updates physical RAM and separate commit");
        main.ApplyLiveMemorySamples(new Dictionary<int, LiveMemorySample> { [42] = sample with { StartUtc = start.AddSeconds(1) } });
        Check(row.MemoryDisplayText == "N/D" && ((SelectableText)main.FindName("DetailMemoryText")).Text == "N/D", "recycled PID invalidates list and selected detail");
        main.ApplyLiveMemorySamples(new Dictionary<int, LiveMemorySample> { [42] = sample with { CapturedAtMonotonicSeconds = now - 7 } });
        Check(row.MemoryDisplayText == "N/D", "expired sample is not republished");
        main.ApplyLiveMemorySamples(new Dictionary<int, LiveMemorySample>());
        Check(((SelectableText)main.FindName("DetailWorkingSetText")).Text == "N/D", "missing acquisition clears selected counters");
        int minimizeRequests = 0; main.MinimizeRequested += () => minimizeRequests++;
        var button = (Button)main.FindName("MinimizeWindowButton");
        Check(button.Content.ToString() == "Nascondi accanto all’orologio" && button.Style is not null, "explicit styled minimize action exists");
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(minimizeRequests == 1, "header click routes to tray controller rather than closing window");
        main.Close(); Application.Current.Shutdown();
        Console.WriteLine("R24 WPF fixture complete: no live acquisition, user programs, clipboard or tray icon. Real shell/DPI verification is separate.");
    }
}
