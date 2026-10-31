namespace ProcessLens.Guardian.Core;

public enum GuardianKind { ProcessStarted, ProcessExited, ProcessObservedAtStartup, ResourceSample, PortOpened, PortClosed, PortObservedAtStartup, Diagnostic }
public sealed record GuardianEvent(
    DateTime Utc, GuardianKind Kind, int Pid = 0, long ProcessStartTicks = 0,
    string Name = "", int ParentPid = 0, string Source = "", double? CpuPercent = null,
    long? ResidentWorkingSetBytes = null, long? PrivateCommitBytes = null,
    string Protocol = "", string LocalAddress = "", int LocalPort = 0,
    string RemoteAddress = "", int RemotePort = 0, string State = "", string Detail = "");

public readonly record struct ProcessIdentity(int Pid, long StartTicks);
public readonly record struct PortIdentity(int Pid, string Protocol, string LocalAddress, int LocalPort,
    string RemoteAddress, int RemotePort, string State);

public sealed record ProcessObserved(int Pid, long StartTicks, int ParentPid, string Name,
    double? CpuPercent, long? ResidentWorkingSetBytes, long? PrivateCommitBytes);

public sealed class GuardianSettings
{
    public int SampleSeconds { get; set; } = 15;
    public int PortScanSeconds { get; set; } = 60;
    public int RetentionDays { get; set; } = 14;
    public int MaxDatabaseMb { get; set; } = 256;
    public bool CaptureRemoteAddresses { get; set; } = false;
    public bool EnableEtw { get; set; } = true;
    public bool EnablePortMonitoring { get; set; } = true;

    public void Normalize()
    {
        SampleSeconds = Math.Clamp(SampleSeconds, 10, 120);
        PortScanSeconds = Math.Clamp(PortScanSeconds, 30, 300);
        RetentionDays = Math.Clamp(RetentionDays, 1, 30);
        MaxDatabaseMb = Math.Clamp(MaxDatabaseMb, 64, 1024);
    }
}

public static class GuardianMath
{
    public static double? ProcessCpuPercent(TimeSpan current, TimeSpan previous, double elapsedSeconds, int logicalProcessors)
    {
        double delta = (current - previous).TotalSeconds;
        if (delta < 0 || elapsedSeconds <= 0 || logicalProcessors <= 0) return null;
        return Math.Clamp(100 * delta / (elapsedSeconds * logicalProcessors), 0, 100);
    }

    public static long FiveMinuteBucket(DateTime utc) => new DateTimeOffset(utc.ToUniversalTime()).ToUnixTimeSeconds() / 300 * 300;

    public static string ExplanationForPort(PortIdentity port)
    {
        if (port.Protocol.StartsWith("TCP", StringComparison.OrdinalIgnoreCase) && port.State == "LISTEN")
            return port.LocalAddress == "127.0.0.1" || port.LocalAddress == "::1"
                ? "Porta in ascolto solo su loopback: non prova esposizione verso Internet."
                : "Porta in ascolto su interfaccia non loopback: verificare firewall e programma; non e' di per se' una minaccia.";
        return "Endpoint rilevato in un campionamento: non dimostra traffico effettivo.";
    }
}

// Thread-safe reconciliation. ETW events and periodic enumeration can race; never use bare PID as durable identity.
public sealed class ProcessEventReconciler
{
    private readonly object _lock = new();
    private readonly Dictionary<int, (long Start, int Parent, string Name)> _known = new();
    private bool _initialized;

    public IReadOnlyList<GuardianEvent> Reconcile(IReadOnlyList<ProcessObserved> observed, DateTime utc)
    {
        lock (_lock)
        {
            var result = new List<GuardianEvent>();
            var seen = new HashSet<int>();
            foreach (var proc in observed)
            {
                seen.Add(proc.Pid);
                if (_known.TryGetValue(proc.Pid, out var prior) && prior.Start == 0)
                {
                    _known[proc.Pid] = (proc.StartTicks, proc.ParentPid, proc.Name);
                    continue;
                }
                if (_known.TryGetValue(proc.Pid, out prior) && prior.Start != proc.StartTicks)
                    result.Add(new(utc, GuardianKind.ProcessExited, proc.Pid, prior.Start, prior.Name, prior.Parent, "poll_pid_reuse"));
                if (!_known.TryGetValue(proc.Pid, out prior) || prior.Start != proc.StartTicks)
                {
                    result.Add(new(utc, _initialized ? GuardianKind.ProcessStarted : GuardianKind.ProcessObservedAtStartup,
                        proc.Pid, proc.StartTicks, proc.Name, proc.ParentPid, "poll"));
                    _known[proc.Pid] = (proc.StartTicks, proc.ParentPid, proc.Name);
                }
            }
            foreach (var (pid, previous) in _known.ToArray())
            {
                if (seen.Contains(pid)) continue;
                result.Add(new(utc, GuardianKind.ProcessExited, pid, previous.Start, previous.Name, previous.Parent, "poll"));
                _known.Remove(pid);
            }
            _initialized = true;
            return result;
        }
    }

    public GuardianEvent? FromEtw(bool isStart, int pid, int parentPid, string name, DateTime utc)
    {
        lock (_lock)
        {
            if (pid <= 0) return null;
            if (isStart)
            {
                // Do not emit if snapshot already recorded an instance. ETW timestamp is
                // not used as start identity: a poll will supply the validated creation time.
                if (_known.ContainsKey(pid)) return null;
                // Unknown start timestamp sentinel (0) is upgraded at next poll, without
                // issuing a second 'started' event for the same PID.
                _known[pid] = (0, parentPid, name);
                return new(utc, GuardianKind.ProcessStarted, pid, 0, name, parentPid, "etw");
            }
            if (!_known.Remove(pid, out var former)) return null;
            return new(utc, GuardianKind.ProcessExited, pid, former.Start, former.Name, former.Parent, "etw");
        }
    }
}

public sealed class PortChangeDetector
{
    private HashSet<PortIdentity>? _previous;
    public IReadOnlyList<GuardianEvent> Reconcile(IReadOnlyCollection<PortIdentity> current, DateTime utc)
    {
        var now = current.ToHashSet();
        var output = new List<GuardianEvent>();
        if (_previous is null)
            foreach (var port in now) output.Add(ToEvent(port, GuardianKind.PortObservedAtStartup, utc));
        else
        {
            foreach (var port in now.Except(_previous)) output.Add(ToEvent(port, GuardianKind.PortOpened, utc));
            foreach (var port in _previous.Except(now)) output.Add(ToEvent(port, GuardianKind.PortClosed, utc));
        }
        _previous = now;
        return output;
    }
    private static GuardianEvent ToEvent(PortIdentity p, GuardianKind kind, DateTime utc) => new(utc, kind, p.Pid,
        Protocol:p.Protocol, LocalAddress:p.LocalAddress, LocalPort:p.LocalPort,
        RemoteAddress:p.RemoteAddress, RemotePort:p.RemotePort, State:p.State,
        Source:"iphlpapi_snapshot", Detail:GuardianMath.ExplanationForPort(p));
}
