using ProcessLens.Core;

namespace ProcessLens.App;

/// <summary>Stores samples inline; allocates public observation objects only for a requested trend.</summary>
internal sealed class MemoryObservationBuffer
{
    private readonly Queue<(DateTime TimestampUtc, long Bytes)> _samples = new();
    private DateTime? _lastTimestamp;
    public int Count => _samples.Count;

    public void Add(DateTime timestampUtc, long bytes)
    {
        if (bytes < 0 || (_lastTimestamp.HasValue && timestampUtc <= _lastTimestamp.Value)) return;
        _lastTimestamp = timestampUtc;
        _samples.Enqueue((timestampUtc, bytes));
        DateTime cutoff = timestampUtc - TimeSpan.FromMinutes(5);
        while (_samples.Count > 0 && (_samples.Peek().TimestampUtc < cutoff || _samples.Count > 160))
            _samples.Dequeue();
    }

    public MemoryObservation[] ToArray()
    {
        var observations = new MemoryObservation[_samples.Count];
        int index = 0;
        foreach (var sample in _samples)
            observations[index++] = new MemoryObservation(sample.TimestampUtc, sample.Bytes);
        return observations;
    }
}
