using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;

namespace ProcessLens.App;

// Observes the owned window only. No process inventory, screenshots or keyboard text.
internal sealed class WindowActionDiagnostics : IDisposable
{
    private readonly Window _window;
    private readonly bool _persist;
    private readonly Button _button;
    private readonly WindowDiagnosticJournal _journal;
    private readonly System.Threading.Timer _probe;
    private readonly object _probeGate = new();
    private readonly MouseButtonEventHandler _mouse;
    private readonly KeyEventHandler _key;
    private long _attempt;
    private bool _disposed;
    private long _inputTicks;
    private Window? _logWindow;
    private bool _logHiddenForOwner;
    private WindowState _logRestoreState = WindowState.Normal;
    private bool _logSyncQueued;
    internal bool LogVisible => _logWindow?.IsVisible == true;
    internal string Report => "ProcessLens · diagnostica finestra e icona R32" + Environment.NewLine +
        "Versione: " + typeof(WindowActionDiagnostics).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion + Environment.NewLine +
        "File: " + AppDiagnostics.WindowLogFile + Environment.NewLine +
        (_persist ? AppDiagnostics.WindowTraceStatus : "Modalità prova: nessuna scrittura del log su disco.") + Environment.NewLine +
        "Icona accanto all’orologio: TRAY_CALLBACK → TRAY_DECISION → stato finestra." + Environment.NewLine +
        "Pulsante nella finestra: INPUT → CLICK → stato finestra." + Environment.NewLine +
        "Ultimi 256 eventi. UI_CHECK_QUEUED senza UI_CHECK: controllo UI ancora in attesa. I log descrivono i passaggi, non provano da soli la causa." + Environment.NewLine + Environment.NewLine + _journal.Snapshot();

    internal WindowActionDiagnostics(Window window, Button button, bool persist)
    {
        _window = window; _button = button; _persist = persist;
        _journal = new(persist ? AppDiagnostics.RecordWindowTrace : null);
        _probe = new System.Threading.Timer(Probe, null, Timeout.Infinite, Timeout.Infinite);
        _mouse = (_, e) => {
            if (e.ChangedButton != MouseButton.Left) return;
            var point = e.GetPosition(_button);
            if (point.X < 0 || point.Y < 0 || point.X > _button.ActualWidth || point.Y > _button.ActualHeight) return;
            Begin("INPUT", $"mouse; gestito={e.Handled}; destinazione={e.OriginalSource?.GetType().Name}; cattura={Mouse.Captured?.GetType().Name ?? "nessuna"}");
            _inputTicks = Environment.TickCount64;
        };
        _key = (_, e) => {
            if (e.Key is Key.Space or Key.Return && _button.IsKeyboardFocusWithin) {
                Begin("INPUT", $"tastiera; gestito={e.Handled}"); _inputTicks = Environment.TickCount64;
            }
        };
        // handledEventsToo observes swallowed input, without handling or changing it.
        _window.AddHandler(UIElement.PreviewMouseDownEvent, _mouse, true);
        _window.AddHandler(UIElement.PreviewKeyDownEvent, _key, true);
        _window.StateChanged += StateChanged;
        _window.IsVisibleChanged += VisibilityChanged;
        _window.Closed += Closed;
        Record("SESSIONE", $"PID={Environment.ProcessId}; R32; log locale; nessuna acquisizione processi/rete");
    }
    internal void BeginTrayAttempt(string detail)
    {
        Begin("TRAY_INPUT", detail);
        Snapshot("TRAY_BEFORE");
    }
    internal void Click()
    {
        if (_inputTicks == 0 || Environment.TickCount64 - _inputTicks > 2000) Begin("CLICK", "pulsante; nessun INPUT recente (possibile attivazione da automazione)");
        else { Record("CLICK", "pulsante Nascondi accanto all’orologio"); ArmProbe(); }
        _inputTicks = 0;
        Snapshot("PRIMA_CLICK");
    }
    private void Begin(string stage, string detail)
    {
        Interlocked.Increment(ref _attempt);
        Record(stage, detail); ArmProbe();
    }
    private void ArmProbe()
    {
        lock (_probeGate) { if (!_disposed) _probe.Change(2000, Timeout.Infinite); }
    }
    private void Probe(object? state)
    {
        lock (_probeGate)
        {
            if (_disposed) return;
            long attempt = Interlocked.Read(ref _attempt);
            _journal.Record(attempt, "UI_CHECK_QUEUED", "controllo dopo 2 s; lettura dello stato UI in attesa");
            try {
                _window.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() => {
                    if (_disposed) return;
                    if (attempt != Interlocked.Read(ref _attempt)) { _journal.Record(attempt, "UI_CHECK", "superato da un tentativo successivo; nessuno stato attribuito"); return; }
                    Snapshot("UI_CHECK");
                }));
            } catch (Exception ex) { _journal.Record(attempt, "UI_CHECK_ERROR", ex.GetType().Name); }
        }
    }
    internal void Record(string stage, string detail) => _journal.Record(Interlocked.Read(ref _attempt), stage, detail);
    internal void Snapshot(string stage)
    {
        try {
            var hwnd = new WindowInteropHelper(_window).Handle;
            Record(stage, $"HWND={hwnd.ToInt64():X}; WPF={_window.WindowState}; visibile={_window.IsVisible}; " +
                $"iconica={(hwnd != IntPtr.Zero && IsIconic(hwnd))}; visibileWin32={(hwnd != IntPtr.Zero && IsWindowVisible(hwnd))}; " +
                $"primoPiano={(hwnd != IntPtr.Zero && GetForegroundWindow() == hwnd)}; barra={_window.ShowInTaskbar}; " +
                $"attiva={_window.IsActive}; logVisibile={LogVisible}; figlieVisibili={_window.OwnedWindows.Cast<Window>().Count(x => x.IsVisible)}; pulsanteAbilitato={_button.IsEnabled}; modalita={_window.ResizeMode}; finestreFiglie={_window.OwnedWindows.Count}");
        } catch (Exception ex) { Record("SNAPSHOT_ERROR", ex.GetType().Name); }
    }
    internal void NativeMessage(int message, long parameter)
    {
        if (message == 0x0112) {
            long command = parameter & 0xFFF0;
            if (command == 0xF020) Begin("SYSCOMMAND", "SC_MINIMIZE; comando nativo lasciato a Windows");
            else if (command is 0xF120 or 0xF030 or 0xF060) Record("SYSCOMMAND", $"comando=0x{command:X}; lasciato a Windows");
        } else if (message == 0x0010) Begin("CLOSE", "WM_CLOSE; X segue la politica di riduzione");
        else if (message == 0x0005 && parameter is >= 0 and <= 2) Record("WM_SIZE", $"tipo={parameter} (0 ripristino, 1 riduzione, 2 ingrandimento)");
    }
    private void StateChanged(object? sender, EventArgs e)
    {
        Snapshot("STATE");
        if (_disposed || _logSyncQueued) return;
        _logSyncQueued = true;
        _window.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() => {
            _logSyncQueued = false;
            if (!_disposed) SetOwnerMinimized(!_window.IsVisible || _window.WindowState == WindowState.Minimized);
        }));
    }
    internal void SetOwnerMinimized(bool minimized)
    {
        try
        {
            if (minimized && _logWindow?.IsVisible == true) {
                _logHiddenForOwner = true; _logWindow.Hide(); Record("LOG_WINDOW", "nascosto con la finestra principale");
            } else if (!minimized && _logHiddenForOwner && _logWindow is not null) {
                _logHiddenForOwner = false; _logWindow.ShowActivated = false; _logWindow.WindowState = _logRestoreState; _logWindow.Show();
                Record("LOG_WINDOW", "riaperto con la finestra principale, senza richiedere il primo piano");
            }
        }
        catch (Exception ex) { Record("LOG_WINDOW_ERROR", ex.GetType().Name); }
    }
    private void VisibilityChanged(object sender, DependencyPropertyChangedEventArgs e) { Snapshot("VISIBILITA"); StateChanged(sender, EventArgs.Empty); }
    private void Closed(object? sender, EventArgs e) => Dispose();
    internal void ShowLog()
    {
        if (_logWindow is not null) { _logWindow.Activate(); return; }
        var panel = CreateLogContent();
        var log = new Window { Owner = _window, Title = "ProcessLens · Log finestra", Width = 900, Height = 540,
            MinWidth = 420, MinHeight = 260, Content = panel, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        _logRestoreState = WindowState.Normal;
        log.StateChanged += (_, _) => { if (log.WindowState != WindowState.Minimized) _logRestoreState = log.WindowState; };
        _logWindow = log; log.Closed += (_, _) => { _logWindow = null; _logHiddenForOwner = false; };
        log.Show(); // Modeless: it must not disable the Minimize button being diagnosed.
    }
    internal DockPanel CreateLogContent()
    {
        var text = new TextBox { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            FontFamily = new System.Windows.Media.FontFamily("Consolas"), FontSize = 13, Margin = new Thickness(8), Text = Report };
        var controls = new WrapPanel { Margin = new Thickness(6) };
        Button ActionButton(string label, Action action) {
            var b = new Button { Content = label, Margin = new Thickness(2), Padding = new Thickness(10, 7, 10, 7),
                Style = _window.TryFindResource("ActionButtonStyle") as Style };
            b.Click += (_, _) => action(); controls.Children.Add(b); return b;
        }
        ActionButton("Aggiorna", () => text.Text = Report);
        ActionButton("Copia tutto", () => ClipboardText.Copy(_logWindow!, text.Text));
        var panel = new DockPanel(); DockPanel.SetDock(controls, Dock.Top); panel.Children.Add(controls); panel.Children.Add(text);
        return panel;
    }
    public void Dispose()
    {
        lock (_probeGate) { if (_disposed) return; _disposed = true; _probe.Dispose(); }
        _window.RemoveHandler(UIElement.PreviewMouseDownEvent, _mouse);
        _window.RemoveHandler(UIElement.PreviewKeyDownEvent, _key);
        _window.StateChanged -= StateChanged; _window.IsVisibleChanged -= VisibilityChanged; _window.Closed -= Closed;
        _logWindow?.Close(); _logWindow = null;
    }
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsIconic(IntPtr hwnd);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
}
