using System.Drawing;
using System.Windows.Forms;
using ProcessLens.Core;
using ProcessLens.Windows;

namespace ProcessLens.MemoryAgent;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        using var mutex = new Mutex(true, @"Local\ProcessLens.MemoryAgent.v1", out bool first);
        if (!first) return;
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        using var context = new MonitorContext();
        Application.Run(context);
    }
}

internal sealed class MonitorContext : ApplicationContext
{
    private readonly MemoryAlertPolicy _policy = new(85, 80);
    private readonly NotifyIcon _icon;
    private readonly System.Windows.Forms.Timer _timer;
    private DateTime _lastErrorLoggedUtc = DateTime.MinValue;
    private bool _previouslyArmed = true;

    public MonitorContext()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Mostra utilizzo RAM", null, (_, _) => ShowCurrent());
        menu.Items.Add("Prova notifica", null, (_, _) => TestNotification());
        menu.Items.Add("Chiudi monitor", null, (_, _) => ExitThread());
        _icon = new NotifyIcon
        {
            Icon = SystemIcons.Information,
            Text = "ProcessLens - avvisi RAM 85% / riarmo 80%",
            Visible = true,
            ContextMenuStrip = menu
        };
        _icon.DoubleClick += (_, _) => ShowCurrent();
        _timer = new System.Windows.Forms.Timer { Interval = 10_000 };
        _timer.Tick += (_, _) => CheckMemory();
        _timer.Start();
        Log("Monitor RAM avviato nella sessione utente; controllo ogni 10 secondi.", eventId: DiagnosticEventIds.RamAgentLifecycle);
        CheckMemory();
    }

    private void CheckMemory()
    {
        try
        {
            var m = WindowsPhysicalMemoryReader.Read();
            _icon.Text = $"ProcessLens RAM: {m.UsedPercent:F1}% usata";
            bool notify = _policy.Observe(m.UsedPercent);
            if (notify)
            {
                string text = $"RAM fisica {m.UsedPercent:F1}%: " +
                    $"{m.UsedBytes / 1073741824d:F1} di {m.TotalBytes / 1073741824d:F1} GiB utilizzati. " +
                    "Controlla i processi in ProcessLens > Memoria RAM.";
                try { _icon.ShowBalloonTip(10_000, "ProcessLens - memoria RAM elevata", text, ToolTipIcon.Warning); }
                catch (Exception ex) { Log("Notifica RAM non visualizzata; motivo tecnico: " + ex.GetType().Name, "AVVISO", ex, DiagnosticEventIds.RamNotificationFailure); }
                Log("Soglia RAM raggiunta (85%): uso attuale " + m.UsedPercent.ToString("F1") + "%. Avviso richiesto a Windows.", "AVVISO", eventId: DiagnosticEventIds.RamThreshold, measuredPercent: m.UsedPercent);
            }
            if (!_previouslyArmed && _policy.Armed)
                Log("Allarme RAM riarmato: uso sceso sotto l'80% (" + m.UsedPercent.ToString("F1") + "%).", eventId: DiagnosticEventIds.RamThreshold, measuredPercent: m.UsedPercent);
            _previouslyArmed = _policy.Armed;
        }
        catch (Exception ex)
        {
            if (DateTime.UtcNow - _lastErrorLoggedUtc > TimeSpan.FromMinutes(15))
            {
                _lastErrorLoggedUtc = DateTime.UtcNow;
                Log("Lettura RAM non disponibile; API GlobalMemoryStatusEx, tipo errore: " + ex.GetType().Name + ". Campione non disponibile: soglia non rivalutata.", "ERRORE", ex, DiagnosticEventIds.RamMeasurement);
            }
        }
    }

    private void TestNotification()
    {
        try
        {
            _icon.ShowBalloonTip(10_000, "ProcessLens - notifica di prova",
                "Avvisi RAM attivi: avviso >=85%, riarmo dopo <80%. Questa e' una prova, non un allarme reale.",
                ToolTipIcon.Info);
            Log("Notifica di prova richiesta dall'utente; non indica un consumo RAM elevato.");
        }
        catch (Exception ex) { Log("Notifica di prova non visualizzata; tipo errore: " + ex.GetType().Name, "AVVISO", ex, DiagnosticEventIds.RamNotificationFailure); }
    }

    private void ShowCurrent()
    {
        try
        {
            var m = WindowsPhysicalMemoryReader.Read();
            MessageBox.Show($"RAM usata: {m.UsedPercent:F1}% " +
                $"({m.UsedBytes / 1073741824d:F2} / {m.TotalBytes / 1073741824d:F2} GiB).\n" +
                "Allarme >=85%; nuovo allarme dopo un valore <80%.",
                "ProcessLens - RAM fisica", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "ProcessLens - RAM non disponibile",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private static void Log(string message, string level = "INFO", Exception? exception = null,
        string? eventId = null, double? measuredPercent = null)
    {
        var error = exception is null ? default : StructuredDiagnostics.ErrorDetails(exception);
        StructuredDiagnostics.Write("memory-agent", new DiagnosticEvent
        {
            EventId = eventId ?? DiagnosticEventIds.RamMeasurement,
            Component = "ProcessLens.MemoryAgent", Operation = eventId == DiagnosticEventIds.RamNotificationFailure
                ? "Notification.Show" : "PhysicalMemory.ReadOrAlert",
            Api = eventId == DiagnosticEventIds.RamNotificationFailure ? "NotifyIcon.ShowBalloonTip"
                : eventId == DiagnosticEventIds.RamAgentLifecycle ? null : "GlobalMemoryStatusEx",
            Severity = level == "ERRORE" ? "Error" : level == "AVVISO" ? "Warning" : "Information",
            Status = exception is null ? "Completed" : "Failed",
            ReasonCode = exception is null ? (eventId == DiagnosticEventIds.RamThreshold ? "MEMORY_THRESHOLD_TRANSITION" : "AGENT_ACTIVITY")
                : eventId == DiagnosticEventIds.RamNotificationFailure ? "NOTIFICATION_FAILED" : "RAM_READ_FAILED",
            Complete = exception is null,
            Measurements = measuredPercent is null ? null : new Dictionary<string, double> { ["physical_ram_used_percent"] = measuredPercent.Value },
            ExceptionType = exception is null ? null : error.ExceptionType,
            HResultHex = exception is null ? null : error.HResultHex,
            Win32ErrorCode = exception is null ? null : error.Win32ErrorCode
        });
        try
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ProcessLens");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "memory-agent.log");
            if (File.Exists(path) && new FileInfo(path).Length > 65536)
                File.Move(path, path + ".old", true);
            File.AppendAllText(path, DateTimeOffset.Now.ToString("O") + " | " + level + " | MemoryAgent | " + message + Environment.NewLine);
        }
        catch { /* monitoring must not stop because local logging is unavailable */ }
    }

    protected override void ExitThreadCore()
    {
        _timer.Stop();
        _timer.Dispose();
        _icon.Visible = false;
        _icon.Dispose();
        base.ExitThreadCore();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Dispose();
            _icon.Dispose();
        }
        base.Dispose(disposing);
    }
}
