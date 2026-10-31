using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using ProcessLens.App;
using ProcessLens.Core;

internal static class Program
{
    static void Check(bool ok, string label)
    { if (!ok) throw new Exception("FAIL new task UI: " + label); Console.WriteLine("PASS new task UI: " + label); }
    static void Complete(Task task)
    {
        var frame = new DispatcherFrame(); var dispatcher = Dispatcher.CurrentDispatcher;
        bool timedOut = false; var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
        timer.Tick += (_, _) => { timedOut = true; frame.Continue = false; };
        task.ContinueWith(_ => dispatcher.BeginInvoke(new Action(() => frame.Continue = false)), TaskScheduler.Default);
        timer.Start(); Dispatcher.PushFrame(frame); timer.Stop();
        if (timedOut) throw new Exception("New task UI fixture timeout");
        task.GetAwaiter().GetResult();
    }
    [STAThread]
    public static void Main()
    {
        _ = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
        try
        {
            var launcher = new Fixture(); var window = new NewTaskWindow(launcher);
            Check(launcher.Calls == 0, "construction does not start a program");
            Complete(window.RunAsync());
            Check(launcher.Calls == 0 && window.StatusText.Text.Contains("Inserisci"), "empty input stays open without a launch");
            window.ProgramBox.Text = "script.ps1"; Complete(window.RunAsync());
            Check(launcher.Calls == 0, "validation fails before scheduling the launcher");
            window.ProgramBox.Text = @"C:\Program Files\tool.exe"; window.ArgumentsBox.Text = "--token=TOP_SECRET";
            launcher.Failure = true; Complete(window.RunAsync());
            Check(launcher.Calls == 1 && window.RunButton.IsEnabled && window.StatusText.Text == "synthetic failed launch",
                "failed launch leaves editable inputs for correction and retry");
            launcher.Failure = false; launcher.Throw = true; Complete(window.RunAsync());
            Check(!window.StatusText.Text.Contains("TOP_SECRET") && window.RunButton.IsEnabled,
                "unexpected launcher error stays in dialog without leaking raw arguments");
            launcher.Throw = false; launcher.Block = true; Task task = window.RunAsync();
            Check(launcher.Started.Wait(TimeSpan.FromSeconds(5)), "fixture starts on worker thread");
            Check(!window.RunButton.IsEnabled && !window.ProgramBox.IsEnabled && window.StatusText.Text.Contains("corso"),
                "pending launch disables duplicate submission and editing");
            Complete(window.RunAsync()); Check(launcher.Calls == 3, "second click while pending cannot launch a second program");
            NewTaskResult? notice = null; window.TaskStarted += r => notice = r;
            launcher.Release.Set(); Complete(task);
            Check(notice is { Started: true, Pid: 4321 } && !notice.Message.Contains("TOP_SECRET"),
                "confirmed start reports PID through the UI event");
            Check(window.ProgramBox.Text.Length == 0 && window.ArgumentsBox.Text.Length == 0 && window.DirectoryBox.Text.Length == 0,
                "closing successful dialog releases typed command fields");
            Complete(window.RunAsync()); Check(launcher.Calls == 3, "closed dialog cannot launch again");
            var cancelled = new NewTaskWindow(launcher); cancelled.ProgramBox.Text = "notepad.exe"; cancelled.Close();
            Complete(cancelled.RunAsync()); Check(launcher.Calls == 3, "cancel before run never starts a program");
            var main = new MainWindow();
            var button = (Button)main.FindName("NewTaskButton");
            Check(button.Content.ToString() == "Esegui nuova attività" && button.Style is not null,
                "main window exposes a styled new-task action");
            main.Close(); Application.Current.Shutdown();
            launcher.Started.Dispose(); launcher.Release.Dispose();
            Console.WriteLine("New task WPF fixture complete; no real program, native launcher, clipboard or UAC used.");
        }
        finally { SynchronizationContext.SetSynchronizationContext(previous); }
    }
    sealed class Fixture : INewTaskLauncher
    {
        public int Calls; public bool Failure, Throw, Block;
        public ManualResetEventSlim Started = new(false), Release = new(false);
        public NewTaskResult Start(NewTaskRequest request)
        {
            Interlocked.Increment(ref Calls);
            if (Throw) throw new InvalidOperationException("--token=TOP_SECRET");
            if (Failure) return new(false, null, "synthetic failed launch");
            if (Block) { Started.Set(); if (!Release.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException(); }
            return new(true, 4321, "Attività avviata · PID 4321.");
        }
    }
}
