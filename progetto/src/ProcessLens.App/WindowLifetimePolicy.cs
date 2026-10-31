namespace ProcessLens.App;

internal enum WindowCloseAction { Exit, MinimizeToTaskbar }

internal static class WindowLifetimePolicy
{
    public static WindowCloseAction Close(bool exitRequested, bool trayAvailable) =>
        exitRequested ? WindowCloseAction.Exit : WindowCloseAction.MinimizeToTaskbar;
    public static bool DeferMinimize(bool closing, bool nativeCallback) => closing || nativeCallback;
    public static bool PollingActive(bool visible, bool minimized) => visible && !minimized;
}

/// <summary>Coalesces window commands after the current Closing/native callback returns.</summary>
internal sealed class DeferredWindowActions(Action<Action> post, Action minimize, Action restore, Action? hideToTray = null) : IDisposable
{
    private Action? _pending;
    private bool _posted, _disposed;
    public void Minimize() => Schedule(minimize);
    public void MinimizeImmediately()
    {
        if (_disposed) return;
        _pending = null; // A queued restore must not undo this explicit click.
        minimize();
    }
    public void Restore() => Schedule(restore);
    public void HideToTray() => Schedule(hideToTray ?? minimize);
    private void Schedule(Action action)
    {
        if (_disposed) return;
        _pending = action;
        if (_posted) return;
        _posted = true;
        post(() =>
        {
            _posted = false;
            var next = _pending; _pending = null;
            if (!_disposed) next?.Invoke();
        });
    }
    public void Dispose() { _disposed = true; _pending = null; }
}

internal enum TrayActivation { Minimize, Restore }
internal sealed class TrayActivationGate
{
    private double? _lastActivation;
    public TrayActivation? Activate(double now, double doubleClickSeconds, bool visible, bool minimized)
    {
        if (!double.IsFinite(now) || !double.IsFinite(doubleClickSeconds) || doubleClickSeconds < 0) return null;
        if (_lastActivation.HasValue && now >= _lastActivation.Value && now - _lastActivation.Value <= doubleClickSeconds) return null;
        _lastActivation = now;
        return visible && !minimized ? TrayActivation.Minimize : TrayActivation.Restore;
    }
}
