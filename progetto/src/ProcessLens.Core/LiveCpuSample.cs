namespace ProcessLens.Core;

/// <summary>Time-based CPU share. Creation time and elapsed time belong to the same PID read.</summary>
public sealed record LiveCpuSample(int Pid, DateTime? StartUtc, bool Available,
    double Percent, double Average, double Peak, double IntervalSeconds,
    double CumulativeSeconds, bool CounterAvailable, int LogicalProcessors)
{
    public double CapturedAtMonotonicSeconds { get; init; }
    public ProcessSnapshot Apply(ProcessSnapshot p) => !Matches(p) ? Unavailable(p) : p with
    {
        CpuSampleAvailable = Available, CpuPercent = Percent,
        CpuAverage30Seconds = Average, CpuPeak30Seconds = Peak,
        CpuSampleIntervalSeconds = IntervalSeconds,
        CpuLogicalCoreEquivalent = Percent * LogicalProcessors / 100d,
        CpuCumulativeSeconds = CumulativeSeconds, CpuCumulativeAvailable = CounterAvailable
    };
    public bool Matches(ProcessSnapshot p) => p.Pid == Pid && StartUtc.HasValue &&
        p.StartTime.HasValue && StartUtc.Value == p.StartTime.Value.ToUniversalTime();
    public static ProcessSnapshot Unavailable(ProcessSnapshot p) => p with
    {
        CpuSampleAvailable = false, CpuPercent = 0, CpuAverage30Seconds = 0,
        CpuPeak30Seconds = 0, CpuLogicalCoreEquivalent = 0, CpuSampleIntervalSeconds = 0,
        CpuCumulativeAvailable = false, CpuCumulativeSeconds = 0
    };
}

/// <summary>One instance per PID. Input clock is monotonic seconds, not wall-clock time.</summary>
public sealed class CpuIntervalTracker
{
    public const double MaximumGapSeconds = 6;
    private (DateTime Start, long Ticks, double Time, int Cpus)? _previous;
    private readonly Queue<(double End, double Seconds, double Percent)> _history = new();

    public void Reset() { _previous = null; _history.Clear(); }

    public LiveCpuSample Sample(int pid, DateTime startUtc, long totalTicks, double now, int cpus)
    {
        var old = _previous;
        double elapsed = old.HasValue ? now - old.Value.Time : 0;
        bool valid = totalTicks >= 0 && double.IsFinite(now) && cpus > 0;
        bool comparable = valid && old.HasValue && old.Value.Start == startUtc &&
            old.Value.Cpus == cpus && totalTicks >= old.Value.Ticks &&
            elapsed > 0 && elapsed <= MaximumGapSeconds;
        if (!comparable) _history.Clear();
        _previous = valid ? (startUtc, totalTicks, now, cpus) : null;
        if (!comparable)
            return new(pid, startUtc, false, 0, 0, 0, 0,
                valid ? totalTicks / (double)TimeSpan.TicksPerSecond : 0, valid, cpus) { CapturedAtMonotonicSeconds = now };

        double percent = Math.Clamp(100d * (totalTicks - old!.Value.Ticks) /
            (TimeSpan.TicksPerSecond * elapsed * cpus), 0, 100);
        _history.Enqueue((now, elapsed, percent));
        while (_history.Count > 64 || (_history.Count > 0 && _history.Peek().End <= now - 30)) _history.Dequeue();
        // Weight only each interval's intersection with the last 30 seconds.
        double weight = 0, weighted = 0, peak = 0;
        foreach (var x in _history)
        {
            double seconds = Math.Min(x.Seconds, x.End - (now - 30));
            weight += seconds; weighted += seconds * x.Percent; peak = Math.Max(peak, x.Percent);
        }
        return new(pid, startUtc, true, percent, weighted / weight, peak, elapsed,
            totalTicks / (double)TimeSpan.TicksPerSecond, true, cpus) { CapturedAtMonotonicSeconds = now };
    }
}
