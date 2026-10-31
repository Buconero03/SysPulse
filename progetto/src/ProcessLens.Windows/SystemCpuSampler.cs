using ProcessLens.Core;

namespace ProcessLens.Windows;

/// <summary>
/// Samples Windows CPU idle/kernel/user FILETIMEs once per process-table scan.
/// Windows kernel time includes idle time: busy = kernel + user - idle.
/// The same total elapsed processor time is used as the denominator for each PID.
/// </summary>
internal sealed class SystemCpuSampler
{
    private (ulong Idle, ulong Total)? _previous;
    private readonly Queue<(DateTime Time, double Busy, double Duration)> _history = new();
    private DateTime? _previousTime;

    public CpuTelemetry Latest { get; private set; } = new(false, 0, 0, 0, 0, Environment.ProcessorCount, 0, 0);
    public ulong LastDeltaTotalTicks { get; private set; }

    public void Sample(DateTime utcNow)
    {
        LastDeltaTotalTicks = 0;
        if (!NativeMethods.GetSystemTimes(out var idle, out var kernel, out var user))
        {
            Latest = Latest with { HasSample = false };
            return;
        }
        ulong total = kernel.AsUInt64 + user.AsUInt64;
        ulong idleTicks = idle.AsUInt64;
        if (_previous is { } old && total > old.Total && idleTicks >= old.Idle)
        {
            ulong totalDelta = total - old.Total;
            ulong idleDelta = idleTicks - old.Idle;
            if (totalDelta > 0 && idleDelta <= totalDelta)
            {
                LastDeltaTotalTicks = totalDelta;
                double busy = CpuMath.MachineBusyPercent(totalDelta, idleDelta);
                double duration = _previousTime.HasValue ? Math.Max(0.01, (utcNow - _previousTime.Value).TotalSeconds) : 1;
                _history.Enqueue((utcNow, busy, duration));
                while (_history.Count > 0 && utcNow - _history.Peek().Time > TimeSpan.FromSeconds(30))
                    _history.Dequeue();
                Latest = new CpuTelemetry(true, busy,
                    _history.Sum(x => x.Busy * x.Duration) / _history.Sum(x => x.Duration),
                    _history.Max(x => x.Busy), 0, Environment.ProcessorCount, 0, 0);
            }
            else Latest = Latest with { HasSample = false };
        }
        else Latest = Latest with { HasSample = false };
        _previous = (idleTicks, total);
        _previousTime = utcNow;
    }

    public void ReportCoverage(double interval, int sampled, int unavailable)
    {
        Latest = Latest with
        {
            SampleIntervalSeconds = interval,
            ProcessesWithCpuSamples = sampled,
            ProcessesWithoutCpuAccess = unavailable
        };
    }
}
