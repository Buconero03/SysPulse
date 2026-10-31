namespace ProcessLens.App;

internal sealed record ProcessExitIdentity(int Pid, long StartUtcTicks, string Name)
{
    public static ProcessExitIdentity? From(int pid, DateTime? start, string name) =>
        pid > 0 && start.HasValue && start.Value != DateTime.MinValue
            ? new(pid, start.Value.ToUniversalTime().Ticks, name) : null;
}

// UI-thread state; duplicate signals from selection and explicit termination produce one notice.
internal sealed class ProcessExitNotice
{
    private readonly Queue<(int Pid, long Start)> _recent = new();
    public bool WasShown(ProcessExitIdentity identity) => _recent.Contains((identity.Pid, identity.StartUtcTicks));

    public string? Confirm(ProcessExitIdentity identity)
    {
        if (identity.Pid <= 0 || identity.StartUtcTicks <= 0 || WasShown(identity)) return null;
        _recent.Enqueue((identity.Pid, identity.StartUtcTicks));
        while (_recent.Count > 16) _recent.Dequeue();
        return $"Il processo {identity.Name} (PID {identity.Pid}) si è chiuso.";
    }
}
