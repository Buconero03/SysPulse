using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;
using Microsoft.Diagnostics.Tracing.Session;
using Microsoft.Extensions.Logging;
using ProcessLens.Guardian.Core;

namespace ProcessLens.Guardian.Service;

public sealed class EtwProcessMonitor : IDisposable
{
    private readonly ProcessEventReconciler _reconciler;
    private readonly Action<GuardianEvent> _emit;
    private readonly ILogger _log;
    private TraceEventSession? _session;
    private Task? _pump;
    private readonly object _lock = new();
    public EtwProcessMonitor(ProcessEventReconciler reconciler, Action<GuardianEvent> emit, ILogger log)
    { _reconciler = reconciler; _emit = emit; _log = log; }

    public void Start()
    {
        lock (_lock)
        {
            if (_session is not null) return;
            TraceEventSession? session = null;
            try
            {
                // Only PROCESS kernel events. No expensive stacks, file/registry or CPU profiling.
                session = new TraceEventSession("ProcessLensGuardianKernel");
                session.StopOnDispose = true;
                // IMPORTANT: TraceEventSession.Source invokes EnsureStarted() when accessed.
                // Enable the kernel provider BEFORE the first access to Source, otherwise
                // EnableKernelProvider throws "The kernel provider must be enabled first...".
                session.EnableKernelProvider(KernelTraceEventParser.Keywords.Process);
                var source = session.Source;
                source.Kernel.ProcessStart += data =>
                {
                    var ev = _reconciler.FromEtw(true, data.ProcessID, data.ParentID, data.ProcessName, data.TimeStamp.ToUniversalTime());
                    if (ev is not null) _emit(ev);
                };
                source.Kernel.ProcessStop += data =>
                {
                    var ev = _reconciler.FromEtw(false, data.ProcessID, data.ParentID, data.ProcessName, data.TimeStamp.ToUniversalTime());
                    if (ev is not null) _emit(ev);
                };
                _session = session;
                _pump = Task.Factory.StartNew(() =>
                {
                    try { source.Process(); }
                    catch (Exception ex)
                    {
                        _log.LogWarning(ex, "ETW Process() terminato. Guardian usa il fallback periodico.");
                        _emit(new(DateTime.UtcNow, GuardianKind.Diagnostic, Source:"etw", Detail:"ETW listener stopped: periodic polling remains active"));
                    }
                }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
                _log.LogInformation("ETW process start/stop collector active.");
                _emit(new(DateTime.UtcNow, GuardianKind.Diagnostic, Source:"etw",
                    Detail:"ETW kernel provider enabled; process event listener started (delivery not yet independently verified)"));
            }
            catch (Exception ex)
            {
                try { session?.Dispose(); } catch { } _session = null;
                _log.LogWarning(ex, "ETW unavailable (possible kernel session conflict or access denial). Poll fallback active.");
                _emit(new(DateTime.UtcNow, GuardianKind.Diagnostic, Source:"etw",
                    Detail:$"ETW unavailable: {ex.GetType().Name}: {ex.Message}; short-lived processes may be missed"));
            }
        }
    }
    public void Dispose()
    {
        lock (_lock)
        {
            try { _session?.Dispose(); }
            catch (Exception ex) { _log.LogWarning(ex, "Unable to dispose ETW kernel session cleanly."); }
            finally { _session = null; }
        }
        // Kernel listener unblocks when the session is stopped. Never wait indefinitely.
        try { _pump?.Wait(TimeSpan.FromSeconds(2)); } catch { }
    }
}
