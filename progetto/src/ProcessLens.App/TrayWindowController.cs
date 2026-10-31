using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;

namespace ProcessLens.App;

// Instantiated by the real App only. Constructing MainWindow in tests adds no tray icon.
internal sealed class TrayWindowController : IDisposable
{
    private const int TrayMessage = 0x8000 + 69;
    private static readonly uint RestoreMessage = RegisterWindowMessageW("ProcessLens.RestoreWindow.v1");
    private static readonly uint TaskbarCreated = RegisterWindowMessageW("TaskbarCreated");
    private readonly Window _window;
    private readonly HwndSource _source;
    private readonly IntPtr _hwnd;
    private readonly ContextMenu _menu;
    private readonly MenuItem _minimizeMenuItem;
    private bool _menuOpenQueued;
    private nint _trayIcon;
    private readonly List<(Window Window, WindowState State)> _hiddenChildren = new();
    private readonly Action _shutdown;
    private readonly Action<bool> _activity;
    private readonly DeferredWindowActions _actions;
    private WindowState _restoreState = WindowState.Normal;
    private bool _trayPresent, _exitRequested, _disposed;
    private int _transitionGeneration;
    private bool _trayVersion4;
    private readonly TrayActivationGate _trayActivation = new();
    private bool? _lastActive;
    private bool _closing, _activityQueued;
    private int _nativeCallbackDepth;
    private readonly Action<string>? _failure;
    private readonly WindowActionDiagnostics? _diagnostics;

    public TrayWindowController(Window window, Action shutdown, Action<bool> activity, Action<string>? failure = null, WindowActionDiagnostics? diagnostics = null)
    {
        _window = window; _shutdown = shutdown; _activity = activity; _failure = failure; _diagnostics = diagnostics;
        _actions = new(action => window.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, action), Minimize, Restore, HideToTray);
        _hwnd = new WindowInteropHelper(window).EnsureHandle();
        _source = HwndSource.FromHwnd(_hwnd) ?? throw new InvalidOperationException("Finestra Windows non disponibile.");
        _source.AddHook(WindowMessage);
        // Limit the elevated window's message exception to its own tray/restore callbacks.
        AllowCallback(TrayMessage, "tray");
        if (RestoreMessage != 0) AllowCallback(RestoreMessage, "ripristino istanza");
        if (TaskbarCreated != 0) AllowCallback(TaskbarCreated, "riavvio Explorer");
        _menu = new ContextMenu { Placement = PlacementMode.MousePoint };
        var open = new MenuItem { Header = "Mostra ProcessLens" };
        open.Click += (_, _) => { _diagnostics?.BeginTrayAttempt("menu Mostra ProcessLens"); _diagnostics?.Record("RESTORE_SOURCE", "menu Mostra ProcessLens"); RequestRestore(); };
        var minimize = new MenuItem { Header = "Nascondi accanto all’orologio" };
        _minimizeMenuItem = minimize;
        _menu.Opened += MenuOpened;
        minimize.Click += (_, _) => { _diagnostics?.BeginTrayAttempt("menu Nascondi accanto all’orologio"); RequestHideToTray(); };
        var exit = new MenuItem { Header = "Esci da ProcessLens" };
        exit.Click += (_, _) => { AllowExit(); _shutdown(); };
        _menu.Items.Add(open); _menu.Items.Add(minimize); _menu.Items.Add(new Separator()); _menu.Items.Add(exit);
        window.Closing += Closing;
        window.Closed += Closed;
        window.StateChanged += StateChanged;
        window.IsVisibleChanged += VisibilityChanged;
        EnsureTray();
    }

    public void AllowExit() { _exitRequested = true; ++_transitionGeneration; _actions.Dispose(); }

    private void Closing(object? sender, CancelEventArgs e)
    {
        var action = WindowLifetimePolicy.Close(_exitRequested, _trayPresent);
        if (action == WindowCloseAction.Exit) return;
        e.Cancel = true;
        _closing = true;
        try { RequestMinimize(); } finally { _closing = false; }
    }

    public void RequestMinimize()
    {
        _diagnostics?.Record("REQUEST", $"riduzione; dispose={_disposed}; uscita={_exitRequested}; shutdown={_window.Dispatcher.HasShutdownStarted}; chiusura={_closing}; callbackNativo={_nativeCallbackDepth}");
        if (_disposed || _exitRequested || _window.Dispatcher.HasShutdownStarted) return;
        if (!_window.Dispatcher.CheckAccess())
        {
            _window.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, new Action(RequestMinimize));
            return;
        }
        // Only Closing/native callbacks defer native minimize. Notification-area hide has its own queued route.
        if (WindowLifetimePolicy.DeferMinimize(_closing, _nativeCallbackDepth > 0)) {
            _diagnostics?.Record("DEFER", "riduzione dopo il callback nativo/Closing"); _actions.Minimize();
        } else _actions.MinimizeImmediately();
    }

    // Explicit notification-area action. Native caption minimize and X keep their taskbar route.
    internal void RequestHideToTray()
    {
        if (_disposed || _exitRequested || _window.Dispatcher.HasShutdownStarted) return;
        if (!_window.Dispatcher.CheckAccess()) {
            _window.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, new Action(RequestHideToTray)); return;
        }
        _diagnostics?.Record("HIDE_REQUEST", "destinazione=area di notifica");
        _actions.HideToTray(); // Last command wins over a pending restore/minimize.
    }

    private void HideToTray()
    {
        if (_disposed || _exitRequested) return;
        if (!_window.IsEnabled) {
            _diagnostics?.Record("HIDE_ABORT", "finestra modale aperta; chiudere prima il dialogo");
            return;
        }
        // Never remove the taskbar route unless Shell has accepted our notification icon.
        if (!EnsureTray()) {
            _diagnostics?.Record("HIDE_ABORT", "icona non registrata; finestra mantenuta accessibile");
            if (!_window.IsVisible) Restore();
            _failure?.Invoke("Icona accanto all’orologio non disponibile. La finestra resta accessibile dalla barra delle applicazioni.");
            return;
        }
        ++_transitionGeneration;
        _menu.IsOpen = false;
        try {
            _diagnostics?.SetOwnerMinimized(true);
            foreach (Window child in _window.OwnedWindows.Cast<Window>().ToArray()) {
                if (!child.IsVisible) continue;
                _hiddenChildren.Add((child, child.WindowState));
                child.Hide();
            }
            _window.Hide(); // Hide, not WindowState.Minimized: no remaining taskbar button.
            _diagnostics?.Record("HIDE_RESULT", $"tray={_trayPresent}; figlieNascoste={_hiddenChildren.Count}");
            _diagnostics?.Snapshot("HIDDEN_TO_TRAY");
            QueueActivityUpdate();
        } catch (InvalidOperationException ex) {
            _diagnostics?.Record("HIDE_ERROR", ex.GetType().Name);
            Restore();
            _failure?.Invoke("Impossibile nascondere ProcessLens. La finestra è stata ripristinata.");
        }
    }

    private void Minimize()
    {
        if (_disposed || _exitRequested) return;
        _diagnostics?.Snapshot("MINIMIZE_ENTER");
        _diagnostics?.SetOwnerMinimized(true);
        _menu.IsOpen = false;
        _window.ShowInTaskbar = true;
        int generation = ++_transitionGeneration;
        _window.WindowState = WindowState.Minimized;
        // Read the CURRENT handle, rather than assuming the tray registration handle is still current.
        var hwnd = new WindowInteropHelper(_window).Handle;
        int nativeError = 0;
        bool nativeAttempted = hwnd != IntPtr.Zero && !IsIconic(hwnd);
        if (nativeAttempted && !CloseWindow(hwnd)) nativeError = Marshal.GetLastWin32Error();
        _diagnostics?.Record("NATIVE_RESULT", $"fallback CloseWindow usato={nativeAttempted}; erroreWin32={nativeError}");
        _diagnostics?.Snapshot("MINIMIZE_RETURN");
        QueueActivityUpdate();
        _window.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() =>
        {
            if (_disposed || _exitRequested || generation != _transitionGeneration) {
                _diagnostics?.Record("VERIFY_SKIPPED", $"generazione={generation}; corrente={_transitionGeneration}; dispose={_disposed}; uscita={_exitRequested}"); return;
            }
            _diagnostics?.Snapshot("VERIFY_LOADED");
            var current = new WindowInteropHelper(_window).Handle;
            bool iconic = current != IntPtr.Zero && IsIconic(current);
            AppDiagnostics.RecordWindowAction($"Riduzione R28: confermata={iconic}; WPF={_window.WindowState}; handle corrente={current.ToInt64():X}; errore Win32={nativeError}.");
            if (!iconic) _failure?.Invoke("Windows non ha confermato la riduzione. Dettagli salvati nel registro diagnostico.");
        }));
    }

    private void Closed(object? sender, EventArgs e) => Dispose();
    private void StateChanged(object? sender, EventArgs e)
    {
        if (_window.WindowState != WindowState.Minimized) { _restoreState = _window.WindowState; ++_transitionGeneration; }
        QueueActivityUpdate();
    }
    private void VisibilityChanged(object sender, DependencyPropertyChangedEventArgs e) => QueueActivityUpdate();
    private void QueueActivityUpdate()
    {
        if (_disposed || _activityQueued) return;
        _activityQueued = true;
        _window.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() =>
        {
            _activityQueued = false;
            if (!_disposed) UpdateActivity();
        }));
    }
    private void UpdateActivity()
    {
        bool active = WindowLifetimePolicy.PollingActive(_window.IsVisible, _window.WindowState == WindowState.Minimized);
        if (_lastActive == active) return;
        _lastActive = active;
        _activity(active);
    }

    private void Restore()
    {
        if (_disposed || _exitRequested) return;
        _diagnostics?.Snapshot("RIPRISTINO_ENTER");
        ++_transitionGeneration;
        var restoreState = _restoreState;
        _menu.IsOpen = false;
        _window.ShowInTaskbar = true;
        _window.Show();
        var hwnd = new WindowInteropHelper(_window).Handle;
        if (IsIconic(hwnd)) ShowWindow(hwnd, 9); // SW_RESTORE before restoring the saved WPF state.
        _window.WindowState = restoreState;
        bool activated = _window.Activate();
        foreach (var child in _hiddenChildren.ToArray()) {
            try {
                if (child.Window.Owner != _window) continue;
                child.Window.ShowActivated = false;
                child.Window.WindowState = child.State;
                child.Window.Show();
            } catch (InvalidOperationException) { _diagnostics?.Record("CHILD_RESTORE", "finestra già chiusa"); }
        }
        _hiddenChildren.Clear();
        _diagnostics?.SetOwnerMinimized(false);
        AppDiagnostics.RecordWindowAction($"Ripristino finestra: visibile={_window.IsVisible}, stato={_window.WindowState}, primo piano={activated}.");
        QueueActivityUpdate();
    }

    internal void RequestRestore()
    {
        if (_disposed || _exitRequested || _window.Dispatcher.HasShutdownStarted) return;
        _diagnostics?.Record("RIPRISTINO_REQUEST", "richiesta da tray, menu o istanza esistente");
        // Do not call Show reentrantly from a native message received during WPF Closing.
        _actions.Restore();
    }

    private IntPtr WindowMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        _diagnostics?.NativeMessage(message, wParam.ToInt64());
        ++_nativeCallbackDepth;
        try { return ProcessWindowMessage(hwnd, message, wParam, lParam, ref handled); }
        finally { --_nativeCallbackDepth; }
    }

    private IntPtr ProcessWindowMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        // WM_SYSCOMMAND and WM_CLOSE deliberately pass through to WPF/Windows.
        if (RestoreMessage != 0 && message == RestoreMessage)
        {
            if (_disposed || _exitRequested) return IntPtr.Zero;
            _diagnostics?.Record("RESTORE_SOURCE", "messaggio da altra istanza ProcessLens");
            RequestRestore(); handled = true; return new IntPtr(1);
        }
        if (TaskbarCreated != 0 && message == TaskbarCreated)
        {
            _trayPresent = false;
            if (!EnsureTray() && !_window.IsVisible) RequestRestore();
        }
        if (message == TrayMessage)
        {
            var packet = TrayCallbackRouter.Decode(wParam.ToInt64(), lParam.ToInt64(), 1);
            // Log before deciding: ignored mouse events were invisible in R29.
            if (packet.Notification != 0x0200)
                _diagnostics?.Record("TRAY_CALLBACK", $"evento=0x{packet.Notification:X4}; formato={packet.Format}; id={packet.IconId}; nostra={packet.Identified}; versione4Negoziata={_trayVersion4}; wParam=0x{wParam.ToInt64():X}; lParam=0x{lParam.ToInt64():X}; HWND=0x{hwnd.ToInt64():X}");
            if (TrayCallbackRouter.IsActivation(packet))
            {
                double now = System.Diagnostics.Stopwatch.GetTimestamp() / (double)System.Diagnostics.Stopwatch.Frequency;
                var current = new WindowInteropHelper(_window).Handle;
                bool minimized = _window.WindowState == WindowState.Minimized || (current != IntPtr.Zero && IsIconic(current));
                var action = _trayActivation.Activate(now, GetDoubleClickTime() / 1000d, _window.IsVisible, minimized);
                if (action.HasValue) _diagnostics?.BeginTrayAttempt($"clic icona accanto all’orologio; evento=0x{packet.Notification:X4}; azione={action}");
                _diagnostics?.Record("TRAY_DECISION", $"evento=0x{packet.Notification:X4}; azione={action?.ToString() ?? "duplicato ignorato"}; ridotta={minimized}");
                if (action == TrayActivation.Minimize) RequestHideToTray();
                else if (action == TrayActivation.Restore) { _diagnostics?.Record("RESTORE_SOURCE", "clic icona accanto all’orologio"); RequestRestore(); }
            }
            else if (TrayCallbackRouter.IsContextMenu(packet))
            {
                _diagnostics?.Record("TRAY_MENU", "tasto destro; apertura menu");
                OpenTrayMenu();
            }
            else if (packet.Notification != 0x0200)
                _diagnostics?.Record("TRAY_DECISION", packet.Identified ? "evento senza azione" : "ID/formato non corrispondente alla nostra icona");
            handled = true;
        }
        return IntPtr.Zero;
    }

    private void OpenTrayMenu()
    {
        if (_disposed || _exitRequested) return;
        if (_menuOpenQueued || _menu.IsOpen) { _diagnostics?.Record("TRAY_MENU", "seconda notifica dello stesso menu ignorata"); return; }
        _menuOpenQueued = true;
        _window.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, new Action(() => {
            _menuOpenQueued = false;
            if (_disposed || _exitRequested) return;
            _minimizeMenuItem.IsEnabled = _window.IsVisible || _diagnostics?.LogVisible == true;
            _minimizeMenuItem.ToolTip = _minimizeMenuItem.IsEnabled ? "Nasconde ProcessLens e le sue finestre accanto all’orologio." : "ProcessLens è già nascosto. Usa Mostra ProcessLens per riaprirlo.";
            _menu.IsOpen = true;
        }));
    }
    private void MenuOpened(object sender, RoutedEventArgs e)
    {
        // Foreground the menu's own popup, never the minimized application window.
        bool popupActivated = false;
        if (PresentationSource.FromVisual(_menu) is HwndSource popup && popup.Handle != IntPtr.Zero)
            popupActivated = SetForegroundWindow(popup.Handle);
        _diagnostics?.Record("TRAY_MENU_POPUP", $"richiesta primo piano al solo menu: esito={popupActivated}");
        _menu.Focus();
        _diagnostics?.Snapshot("TRAY_MENU_OPENED");
    }

    private void AllowCallback(uint message, string name)
    {
        bool allowed = ChangeWindowMessageFilterEx(_hwnd, message, 1, IntPtr.Zero);
        int error = allowed ? 0 : Marshal.GetLastWin32Error();
        _diagnostics?.Record("TRAY_FILTER", $"{name}; messaggio=0x{message:X}; consentito={allowed}; erroreWin32={error}");
    }

    private NotifyIconData IconData() => new()
    {
        Size = (uint)Marshal.SizeOf<NotifyIconData>(), Window = _hwnd, Id = 1,
        Flags = 1 | 2 | 4 | 0x80, CallbackMessage = TrayMessage,
        Icon = TrayIcon(), Tip = "ProcessLens — clic: nascondi/mostra; tasto destro: menu",
        Info = "", InfoTitle = ""
    };

    private nint TrayIcon()
    {
        if (_trayIcon == 0) {
            try {
                byte[]? file = IconFile.ReadEmbedded(typeof(TrayWindowController).Assembly);
                if (IconFile.TrySelectImage(file, 32, out var bits))
                    _trayIcon = CreateIconFromResourceEx(bits, (uint)bits.Length, true, 0x00030000, 32, 32, 0);
            } catch (Exception ex) { _diagnostics?.Record("TRAY_ICON_ERROR", ex.GetType().Name); }
        }
        return _trayIcon != 0 ? _trayIcon : LoadIconW(IntPtr.Zero, new IntPtr(32512));
    }

    private bool EnsureTray()
    {
        if (_disposed) return false;
        if (_trayPresent)
        {
            var existing = IconData();
            if (Shell_NotifyIconW(1, ref existing) || FindWindowW("Shell_TrayWnd", null) == IntPtr.Zero) { _diagnostics?.Record("TRAY_REGISTER", "icona esistente aggiornata"); return true; } // NIM_MODIFY also detects a lost shell entry.
            _trayPresent = false;
        }
        var data = IconData();
        if (data.Icon == IntPtr.Zero) { _diagnostics?.Record("TRAY_REGISTER", "icona nativa non disponibile"); return false; }
        _trayVersion4 = false;
        if (FindWindowW("Shell_TrayWnd", null) == IntPtr.Zero)
        {
            _trayPresent = true;
            _trayVersion4 = true;
        }
        else
        {
            _trayPresent = Shell_NotifyIconW(0, ref data); // NIM_ADD
            if (_trayPresent)
            {
                data.Version = 4;
                _trayVersion4 = Shell_NotifyIconW(4, ref data); // Decode both documented packet layouts; coalesce duplicate activation events.
            }
        }
        _diagnostics?.Record("TRAY_REGISTER", $"aggiunta={_trayPresent}; versione4={_trayVersion4}; HWND=0x{_hwnd.ToInt64():X}; id=1");
        return _trayPresent;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _actions.Dispose();
        _hiddenChildren.Clear();
        _menu.IsOpen = false;
        _menu.Opened -= MenuOpened;
        _window.Closing -= Closing; _window.Closed -= Closed;
        _window.StateChanged -= StateChanged; _window.IsVisibleChanged -= VisibilityChanged;
        if (!_source.IsDisposed) _source.RemoveHook(WindowMessage);
        if (_trayPresent)
        {
            var data = IconData();
            Shell_NotifyIconW(2, ref data); // NIM_DELETE; LoadIcon returns a shared system icon.
            _trayPresent = false;
        }
        if (_trayIcon != 0) { DestroyIcon(_trayIcon); _trayIcon = 0; }
    }

    public static bool TryRestoreExisting()
    {
        if (RestoreMessage == 0) return false;
        bool restored = false;
        EnumWindows((hwnd, _) =>
        {
            GetWindowThreadProcessId(hwnd, out uint pid);
            if (pid == Environment.ProcessId) return true;
            var title = new StringBuilder(128);
            GetWindowTextW(hwnd, title, title.Capacity);
            if (!string.Equals(title.ToString(), "ProcessLens", StringComparison.Ordinal)) return true;
            AllowSetForegroundWindow(pid);
            if (SendMessageTimeoutW(hwnd, RestoreMessage, IntPtr.Zero, IntPtr.Zero, 2, 500, out var result) != IntPtr.Zero &&
                result == new IntPtr(1)) { restored = true; return false; }
            return true;
        }, IntPtr.Zero);
        return restored;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NotifyIconData
    {
        public uint Size; public IntPtr Window; public uint Id, Flags, CallbackMessage; public IntPtr Icon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Tip;
        public uint State, StateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string Info;
        public uint Version;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string InfoTitle;
        public uint InfoFlags; public Guid Guid; public IntPtr BalloonIcon;
    }
    private delegate bool EnumWindowCallback(IntPtr hwnd, IntPtr parameter);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool Shell_NotifyIconW(uint message, ref NotifyIconData data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern uint RegisterWindowMessageW(string text);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr LoadIconW(IntPtr instance, IntPtr name);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr FindWindowW(string? lpClassName, string? lpWindowName);
    [DllImport("user32.dll")] private static extern uint GetDoubleClickTime();
    [DllImport("user32.dll", SetLastError = true)] private static extern nint CreateIconFromResourceEx(byte[] bits, uint byteCount, [MarshalAs(UnmanagedType.Bool)] bool icon, uint version, int width, int height, uint flags);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool DestroyIcon(nint icon);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseWindow(IntPtr hwnd);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr hwnd, int command);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(IntPtr hwnd);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ChangeWindowMessageFilterEx(IntPtr hwnd, uint message, uint action, IntPtr changeFilter);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hwnd);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AllowSetForegroundWindow(uint pid);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowCallback callback, IntPtr parameter);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowTextW(IntPtr hwnd, StringBuilder text, int max);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr SendMessageTimeoutW(
        IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam, uint flags, uint timeout, out IntPtr result);
}
