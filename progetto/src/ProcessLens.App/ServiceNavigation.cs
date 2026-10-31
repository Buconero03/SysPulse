namespace ProcessLens.App;

public sealed record MemoryDetailsTarget(int Pid, string ExecutableName, bool Grouped, DateTime? FrameStartedUtc = null);
public sealed record ServiceHostCandidate(int Pid, string Name, DateTime? SnapshotStartUtc, DateTime? CurrentStartUtc);
public static class ServiceNavigation
{
    public static bool Matches(MemoryDetailsTarget target, ServiceHostCandidate host) => host.Pid > 0 &&
        host.SnapshotStartUtc.HasValue && host.SnapshotStartUtc == host.CurrentStartUtc &&
        (!target.FrameStartedUtc.HasValue || host.CurrentStartUtc!.Value <= target.FrameStartedUtc.Value) &&
        string.Equals(System.IO.Path.GetFileNameWithoutExtension(host.Name),
            System.IO.Path.GetFileNameWithoutExtension(target.ExecutableName), StringComparison.OrdinalIgnoreCase) &&
        (target.Grouped || host.Pid == target.Pid);
    public static bool Includes(int pid, IReadOnlyDictionary<int, DateTime>? scope) => scope is null || scope.ContainsKey(pid);
}
