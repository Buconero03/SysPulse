using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ProcessLens.Guardian.Core;

namespace ProcessLens.Guardian.Service;

public sealed class GuardianWorker : BackgroundService
{
    private readonly ILogger<GuardianWorker> _logger;
    private readonly GuardianSettings _settings;
    private readonly Channel<GuardianEvent> _queue = Channel.CreateBounded<GuardianEvent>(new BoundedChannelOptions(8192)
    { SingleReader=true, SingleWriter=false, FullMode=BoundedChannelFullMode.Wait });
    private readonly ProcessEventReconciler _reconciler = new();
    private readonly LightweightSampler _sampler = new();
    private readonly PortChangeDetector _ports = new();
    private long _dropped;
    private long _failedStorageEvents;
    private long _acceptedEvents;
    private long _savedEvents;
    private readonly string _dataPath;

    public GuardianWorker(ILogger<GuardianWorker> logger)
    {
        _logger = logger;
        _dataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "ProcessLens", "Guardian");
        string settingsFile = Path.Combine(AppContext.BaseDirectory, "guardian.settings.json");
        try { _settings = File.Exists(settingsFile)
            ? JsonSerializer.Deserialize<GuardianSettings>(File.ReadAllText(settingsFile)) ?? new GuardianSettings()
            : new GuardianSettings(); }
        catch(Exception ex) { logger.LogWarning(ex, "Invalid configuration, applying safe defaults"); _settings = new GuardianSettings(); }
        _settings.Normalize();
    }
    private void Emit(GuardianEvent ev)
    {
        if (_queue.Writer.TryWrite(ev)) Interlocked.Increment(ref _acceptedEvents);
        else Interlocked.Increment(ref _dropped);
    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Directory.CreateDirectory(_dataPath);
        _logger.LogInformation("Guardian collector started. Storage: {Path}", _dataPath);
        Task writing = Task.Run(() => WriteLoop(stoppingToken), CancellationToken.None);
        using var etw = new EtwProcessMonitor(_reconciler, Emit, _logger);
        if (_settings.EnableEtw) etw.Start();
        DateTime nextPorts = DateTime.MinValue;
        try
        {
            // Sample immediately, then periodically; do not busy-wait.
            while (!stoppingToken.IsCancellationRequested)
            {
                // An inoperative SQLite writer must not leave a seemingly healthy
                // collector running indefinitely while the bounded channel overflows.
                if (writing.IsFaulted || writing.IsCanceled)
                    throw new InvalidOperationException("Guardian SQLite writer stopped unexpectedly", writing.Exception);
                DateTime utc = DateTime.UtcNow;
                try
                {
                    var snapshot = _sampler.Capture(utc);
                    foreach (var ev in _reconciler.Reconcile(snapshot, utc)) Emit(ev);
                    foreach (var row in snapshot)
                        Emit(new(utc, GuardianKind.ResourceSample, row.Pid, row.StartTicks, row.Name,
                            CpuPercent:row.CpuPercent, ResidentWorkingSetBytes:row.ResidentWorkingSetBytes,
                            PrivateCommitBytes:row.PrivateCommitBytes, Source:"periodic"));
                }
                catch(Exception ex) { _logger.LogError(ex, "Process sampling error"); Emit(new(utc, GuardianKind.Diagnostic, Source:"sampler",Detail:ex.Message)); }
                if (_settings.EnablePortMonitoring && utc >= nextPorts)
                {
                    nextPorts = utc.AddSeconds(_settings.PortScanSeconds);
                    try { foreach(var ev in _ports.Reconcile(WindowsPortSampler.Capture(_settings.CaptureRemoteAddresses), utc)) Emit(ev); }
                    catch(Exception ex) { _logger.LogWarning(ex, "Network port scan failed"); Emit(new(utc, GuardianKind.Diagnostic, Source:"ports", Detail:ex.Message)); }
                }
                try { await Task.Delay(TimeSpan.FromSeconds(_settings.SampleSeconds), stoppingToken); }
                catch(OperationCanceledException) { break; }
            }
        }
        finally
        {
            etw.Dispose();
            _queue.Writer.TryComplete();
            try { await writing; }
            catch(Exception ex) { _logger.LogError(ex, "Guardian database writer failed"); }
            _logger.LogInformation("Guardian stopped. Accepted: {Accepted}; saved: {Saved}; queue drops: {QueueDrops}; storage write losses: {StorageLosses}",
                Interlocked.Read(ref _acceptedEvents), Interlocked.Read(ref _savedEvents),
                Interlocked.Read(ref _dropped), Interlocked.Read(ref _failedStorageEvents));
        }
    }
    private async Task WriteLoop(CancellationToken stop)
    {
        using var store = new GuardianStore(_dataPath);
        DateOnly lastMaintenanceDay = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);
        DateTime nextMaintenanceAttempt = DateTime.MinValue;
        DateTime nextHealth = DateTime.UtcNow.AddMinutes(10);
        var buffer = new List<GuardianEvent>(512);
        bool completed = false;
        int consecutiveStorageFailures = 0;
        while (!completed)
        {
            // The writer drains after the input channel is completed, independently
            // of the stop token, so graceful service shutdown does not discard events.
            using var flushTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            try
            {
                if (await _queue.Reader.WaitToReadAsync(flushTimeout.Token))
                    while (buffer.Count < 512 && _queue.Reader.TryRead(out var item)) buffer.Add(item);
                else completed = true;
            }
            catch (OperationCanceledException) { }
            if (buffer.Count > 0 && !completed)
            {
                if (buffer.Count < 512)
                {
                    await Task.Delay(100, CancellationToken.None);
                    while (buffer.Count < 512 && _queue.Reader.TryRead(out var grouped)) buffer.Add(grouped);
                }
            }
            if (buffer.Count > 0)
            {
                try
                {
                    store.SaveBatch(buffer);
                    Interlocked.Add(ref _savedEvents, buffer.Count);
                    consecutiveStorageFailures = 0;
                }
                catch (Exception ex)
                {
                    // Transactions are atomic: a failed batch has not been committed.
                    // We do not report zero loss or silently retry duplicate inserts.
                    Interlocked.Add(ref _failedStorageEvents, buffer.Count);
                    _logger.LogError(ex, "Guardian SQLite write FAILED; {Count} events lost in this transaction", buffer.Count);
                    if (++consecutiveStorageFailures >= 5)
                        throw new IOException("Guardian SQLite writer failed five consecutive batches; service must not present stale collection as healthy", ex);
                }
                buffer.Clear();
            }
            // Separate loss counters from the bounded channel, so a full channel
            // cannot prevent accounting for the very events it rejected.
            PersistLossDiagnostics(store);
            DateTime now = DateTime.UtcNow;
            if (now >= nextHealth)
            {
                nextHealth = now.AddMinutes(10);
                try
                {
                    using var self = System.Diagnostics.Process.GetCurrentProcess();
                    string info = $"accepted={Interlocked.Read(ref _acceptedEvents)} saved={Interlocked.Read(ref _savedEvents)} working_set_mib={self.WorkingSet64 / 1048576.0:F1}";
                    store.SaveBatch(new [] { new GuardianEvent(now,GuardianKind.Diagnostic,Source:"health",Detail:info) });
                }
                catch (Exception ex) { _logger.LogWarning(ex, "Health snapshot was not stored"); }
            }
            DateOnly today = DateOnly.FromDateTime(now);
            if (today != lastMaintenanceDay && now >= nextMaintenanceAttempt)
            {
                // Retry failures after an hour instead of repeatedly blocking the
                // writer every two seconds when readers pin a WAL checkpoint.
                nextMaintenanceAttempt = now.AddHours(1);
                try
                {
                    var result = store.Maintenance(_settings, now);
                    if (result.AboveSoftQuota)
                    {
                        string warning = $"Soft quota exceeded: main={result.MainBytes} WAL={result.WalBytes} live_estimate={result.EstimatedLiveBytes} quota={result.QuotaBytes}; protected last hour, pruned_hours={result.PrunedHours}";
                        _logger.LogWarning("{Message}", warning);
                        store.SaveBatch(new [] { new GuardianEvent(now,GuardianKind.Diagnostic,Source:"storage",Detail:warning) });
                    }
                    if (result.CheckpointBusy)
                        _logger.LogWarning("Guardian SQLite WAL checkpoint was busy; data remains in WAL");
                    var prior = today.AddDays(-1);
                    string reports = Path.Combine(_dataPath,"reports");
                    Directory.CreateDirectory(reports);
                    File.WriteAllText(Path.Combine(reports,$"{prior:yyyy-MM-dd}.md"),store.DailyReport(prior));
                    foreach(var file in Directory.GetFiles(reports,"*.md"))
                        if (File.GetCreationTimeUtc(file) < now.AddDays(-_settings.RetentionDays)) File.Delete(file);
                    lastMaintenanceDay = today;
                }
                catch(Exception ex) { _logger.LogError(ex,"Maintenance/report failed. Next attempt in one hour"); }
            }
        }
        // A separate final check also covers drops reported while draining.
        PersistLossDiagnostics(store);
    }

    private void PersistLossDiagnostics(GuardianStore store)
    {
        long rejected = Interlocked.Exchange(ref _dropped, 0);
        long writeLoss = Interlocked.Exchange(ref _failedStorageEvents, 0);
        if (rejected == 0 && writeLoss == 0) return;
        if (rejected > 0)
            _logger.LogWarning("Guardian input queue overflow: {Count} events dropped (capacity 8192)", rejected);
        if (writeLoss > 0)
            _logger.LogWarning("Guardian SQLite transaction failures: {Count} events not saved", writeLoss);
        var diagnostics = new List<GuardianEvent>(2);
        if (rejected > 0) diagnostics.Add(new(DateTime.UtcNow,GuardianKind.Diagnostic,Source:"queue",Detail:$"Dropped {rejected} events under load; loss is explicit"));
        if (writeLoss > 0) diagnostics.Add(new(DateTime.UtcNow,GuardianKind.Diagnostic,Source:"storage",Detail:$"Lost {writeLoss} events due to failed SQLite transactions"));
        try { store.SaveBatch(diagnostics); }
        catch (Exception ex)
        {
            // Preserve counters for the next attempt; the Windows Event Log provides
            // a secondary indication if storage itself is unavailable.
            Interlocked.Add(ref _dropped, rejected);
            Interlocked.Add(ref _failedStorageEvents, writeLoss);
            _logger.LogError(ex, "Guardian loss counters could not be persisted; will retry");
        }
    }
}
