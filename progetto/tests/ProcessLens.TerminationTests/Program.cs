using ProcessLens.Core;

void Check(string label, bool condition)
{
    if (!condition) throw new Exception($"FAIL {label}");
    Console.WriteLine($"PASS {label}");
}

var when = new DateTime(2026, 10, 8, 10, 0, 0, DateTimeKind.Utc);
ProcessSnapshot Snapshot(int pid, string name = "sample.exe", bool critical = false,
    IReadOnlyList<string>? services = null, DateTime? start = null, string? path = @"C:\Apps\sample.exe") =>
    new() { Pid = pid, ParentPid = 1, Name = name, StartTime = start ?? when,
        ExecutablePath = path, IsCritical = critical, ServiceNames = services ?? [] };

Check("ordinary process allowed", ProcessTerminationPolicy.Evaluate(Snapshot(9001), 9000).Allowed);
Check("self denied", !ProcessTerminationPolicy.Evaluate(Snapshot(9000), 9000).Allowed);
Check("system pid denied", !ProcessTerminationPolicy.Evaluate(Snapshot(4), 9000).Allowed);
Check("critical denied", !ProcessTerminationPolicy.Evaluate(Snapshot(9001, critical: true), 9000).Allowed);
Check("lsass name denied", !ProcessTerminationPolicy.Evaluate(Snapshot(9001, "lsass.exe"), 9000).Allowed);
Check("service host denied", !ProcessTerminationPolicy.Evaluate(Snapshot(9001, services: ["SomeService"]), 9000).Allowed);
Check("path absent denied", !ProcessTerminationPolicy.Evaluate(Snapshot(9001, path: ""), 9000).Allowed);
Check("start absent denied", !ProcessTerminationPolicy.Evaluate(Snapshot(9001) with { StartTime = null }, 9000).Allowed);
Check("same timestamp", ProcessTerminationPolicy.IsSameCreationTime(when, when.ToLocalTime()));
Check("reused PID mismatched timestamp", !ProcessTerminationPolicy.IsSameCreationTime(when, when.AddSeconds(3)));
Console.WriteLine("Termination policy tests completed (no process was terminated).");
