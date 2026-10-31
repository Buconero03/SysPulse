using System.Diagnostics;
using System.Globalization;

namespace ProcessLens.App;

// Bounded, local window trace. Timestamps belong to the event, not the later disk write.
internal sealed class WindowDiagnosticJournal(Action<string>? sink = null,
    Func<DateTimeOffset>? utcNow = null, Func<double>? monotonicSeconds = null, int capacity = 256)
{
    private readonly object _gate = new();
    private readonly Queue<string> _lines = new();
    private readonly string _session = Guid.NewGuid().ToString("N")[..8];
    private long _sequence;
    private readonly Func<DateTimeOffset> _utc = utcNow ?? (() => DateTimeOffset.UtcNow);
    private readonly Func<double> _mono = monotonicSeconds ?? (() => Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency);
    private readonly double _start = (monotonicSeconds ?? (() => Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency))();
    internal void Record(long attempt, string stage, string detail)
    {
        lock (_gate)
        {
            static string Safe(string text) => text.Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ');
            string value = Safe(detail);
            if (value.Length > 1400) value = value[..1400] + "…";
            string line = string.Create(CultureInfo.InvariantCulture,
                $"[{_utc():O}] [Finestra] sessione={_session} n={++_sequence} tentativo={attempt} +{Math.Max(0, _mono() - _start):F3}s {Safe(stage)}: {value}");
            if (_lines.Count >= Math.Clamp(capacity, 1, 256)) _lines.Dequeue();
            _lines.Enqueue(line);
            try { sink?.Invoke(line); } catch { /* The command never depends on diagnostics. */ }
        }
    }
    internal string Snapshot() { lock (_gate) return string.Join(Environment.NewLine, _lines); }
}
